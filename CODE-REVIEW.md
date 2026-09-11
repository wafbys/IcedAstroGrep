# IcedAstroGrep 代码评审报告

评审对象：`C:\Users\YF\YF21CN\Src\IcedAstroGrep`（AstroGrep 4.4.9 的 .NET 10 便携版分支）
评审范围：全部 291 个受版本控制的文件（211 个 `.cs`），4 个项目
评审方式：静态阅读 + 实际编译 + 测试执行 + 针对性复现实验

---

## 1. 总体结论

| 维度 | 评价 |
|---|---|
| 架构 | 良好。Core / IFilter / App 三层分离干净，`ISearchSpec` 契约清晰，Core 无 WinForms 依赖（仅 System.Drawing 用于颜色常量），未来可加 WinUI 3 宿主。 |
| 工程化 | **薄弱**。无 CI；测试仅 4 个（约 94 行），相对 Core 约 8000 行而言覆盖率极低；构建垃圾与缓存文件未清理。 |
| 可靠性 | **存在多类严重缺陷**：灾难性回溯正则导致搜索线程永久卡死、编码缓存数据结构不一致、IFilter 互操作资源泄漏与死循环、插件子系统可导致进程无法启动或 OOM。 |
| 安全性 | 作为本地桌面工具，攻击面可控；但"便携"承诺与注册表行为矛盾。 |

构建与测试实测结果（本机 .NET SDK 10.0.401）：

- `dotnet build`（单节点）**成功**，0 warning / 0 error。
- `dotnet build IcedAstroGrep.slnx`（默认多节点）**失败**且无错误信息输出——这是环境/沙箱问题，不是代码问题，见 §5。
- 4 个单元测试**全部通过**（用自建 harness 执行，因为 `dotnet test` 的 vstest 宿主在本沙箱被拒绝，见 §5）。

---

## 2. 严重问题（P0）

### P0-1 正则表达式可导致搜索线程永久挂死（已复现）

`src/IcedAstroGrep.Core/Grep.cs:208-230`

```csharp
var options = searchSpec.UseCaseSensitivity ? RegexOptions.None : RegexOptions.IgnoreCase | RegexOptions.Compiled;
...
regEx = new Regex(pattern, options);   // 无 matchTimeout 参数
```

- 全仓库没有任何一处给 `Regex` 传 `matchTimeout`；`Grep.BuildSearchRegEx`、`IFilterPlugin.cs:134`、`FilterSearcher.cs:110`、`frmMain.cs:4970` 都是无超时构造。
- `frmMain.cs:4965-4978` 的输入校验只捕获"语法非法"，不做复杂度/耗时校验。
- 搜索运行在 `BeginExecute()` 起的后台线程上，取消只在**文件之间**检查（`ThrowIfCancellationRequested`），单行匹配内部无法中断。

实测（复现 `(a+)+$` 作用于 31 字符输入）：

```
pattern (a+)+$: completed in 128664 ms
pattern ^(a+)+$: completed in 80434 ms
```

用户输入一个常见的手写错误模式（如 `(a+)+$`、`(\w+\s?)*$`）搜索任意目录，程序会无限期无响应，且"取消"按钮无效——因为 `Abort()` 只能置位 token，而线程卡在 `Regex.Matches` 里。这是本项目中风险最高的缺陷。

修复建议：
1. 所有 `Regex` 构造传入 `TimeSpan`（例如 2 秒），并把 `RegexMatchTimeoutException` 转换为可见的搜索错误。
2. 在验证界面用一个"探针"字符串试跑一次，超时即拒绝该模式。
3. 顺带修一个性能问题：`BuildSearchRegEx` 在**每个文件**里都会被调用一次（`SearchFileContents` 开头），配合 `RegexOptions.Compiled` 意味着每个文件重新做一次动态 IL 编译；应改为每次搜索编译一次。

### P0-2 `EncodingCache` 数据结构不一致 + 无任何线程同步

`src/IcedAstroGrep.Core/EncodingDetection/Caching/EncodingCache.cs:117-124`

```csharp
public void RemoveItem(string key)
{
    if (cache.ContainsKey(key))
    {
        lruList.Remove(key);      // 只从 LRU 链表移除，字典里的条目从未删除
    }
}
```

后果链：
1. 文件大小变化时 `Grep.cs:961-964` 调用 `RemoveItem`，字典条目残留，`Save()` 会把过期条目写回磁盘。
2. 因为 `cache.Count` 的增长与 `lruList.Count` 脱钩，`SetItem` 的淘汰逻辑（`cache.Remove(lruList.First.Value)`，第 151-156 行）走在一份与实际内容不匹配的 LRU 上，长期运行会出现随机的淘汰错位。
3. `SetItem` 第 151-156 行在**单次插入多于一**时可能访问 `lruList.First` 而链表为空 → `InvalidOperationException`，直接落进 `SearchFile` 的通用异常处理，表现为"搜索中途报错/结果缺失"。
4. `cache`/`lruList` 是普通 `Dictionary`/`LinkedList`，而 `SetItem` 由**搜索后台线程**调用，`frmMain.StartSearch` 又不会等待上一次搜索结束（见 P0-3），因此存在真正的并发写 `Dictionary` —— 可能抛异常，也可能破坏其内部结构。

修复建议：`RemoveItem` 同时 `cache.Remove(key)`；给缓存加锁或用 `ConcurrentDictionary`；把淘汰改成基于 `cache.Count` 的自洽实现。

### P0-3 允许同时运行两个搜索，`Grep` 实例被静默丢弃

`src/IcedAstroGrep.App/Windows/Forms/frmMain.cs:4663-4678`

```csharp
if (__Grep != null)
{
    __Grep.FileHit -= ReceiveFileHit;   // 只解绑事件
    ...
    __Grep = null;                      // 没有 Abort()，也不等待线程结束
}
```

"取消"（`btnCancel_Click`）走的是 `Abort()`，但**再次点击"搜索"**时既不中止旧线程也不等待它。旧线程继续跑完整个目录树，同时：
- 与新的搜索竞争 §P0-2 中的全局 `EncodingCache`；
- 旧 `Grep` 在 `finally` 里调用 `EncodingCache.Save()`（`Grep.cs:1349-1352`），与新搜索的 load/save 竞争同一个文件；
- 期间两次搜索都持有 `PluginManager.Items` 中的**同一批插件实例**并调用 `Load()/Unload()`。

修复建议：`StartSearch` 开头调用 `__Grep.Abort()` 并 join（或加 `IsBusy` 互斥，搜索期间禁用"搜索"按钮）。

---

## 3. 重要问题（P1）

### P1-1 `IFilterTextReader`：公开 API `Read()` 必然抛异常

`src/IcedAstroGrep.IFilter/FilterReader.cs:387-397`

```csharp
var chr = new char[0];
var read = Read(chr, 0, 1);   // Read(char[],int,int) 第 424 行：buffer.Length - index < count → 0 < 1 成立
```

`Read()` 每次调用都会抛 `ArgumentException("The buffer is to small")`；即使绕过长度检查，下一行 `chr[0]` 也会越界。修复：`new char[1]`。

### P1-2 `IFilterTextReader`：多条"无进展"路径造成永久死循环

`FilterReader.cs:438-658`，配合 `FilterReaderOptions.ReaderTimeout` 默认 `NoTimeout`：

- `switch (_chunk.flags)` 没有 `default` 分支；`[Flags]` 枚举合法地可能为 0 或其他未枚举组合 → 一个 case 都不命中，`_chunkValid` 保持 true，在纯托管代码里 100% CPU 空转。
- `GetText` 只对部分 HRESULT 清 `_chunkValid`；`FILTER_E_NO_TEXT`、`E_FAIL`、`E_HANDLE`、`E_INVALIDARG` 等会被无限重试。
- `S_OK` 且 `*pcwcBuffer == 0`、无 break char、属性集未变 → `charsRead` 不前进，循环不终止（`ReadToEnd` 会顺带 OOM）。
- `Timeout()` 只能在两次迭代之间生效，无法中断阻塞在原生调用里的滤镜。

威胁模型正好命中：grep 工具必须能承受畸形文件 + 行为异常的第三方 iFilter。修复：两个 `switch` 都补 `default:` 并清 `_chunkValid`；把"零进展"作为防御性终止条件；默认启用显式超时。

### P1-3 `IFilterTextReader`：非托管资源泄漏 / 用错释放函数

`FilterReader.cs:524-561`：`IFilter::GetValue` 的 `PROPVARIANT**` 是滤镜用 `CoTaskMemAlloc` 返回的，代码预先 `Marshal.AllocHGlobal` 的那块被覆盖后**永不释放**（每个 value chunk 泄漏约 24 字节，长搜索无上限累积）；随后又用 `Marshal.FreeHGlobal` 去释放 `CoTaskMemAlloc` 的内存（释放函数不匹配），且未对其调用 `PropVariantClear`。

`FilterLoader.cs:162-172`：`stream.Read(buffer, 0, buffer.Length)` 单次读取不保证填满（`Stream` 契约），剩余部分变成零字节喂给滤镜；`CreateStreamOnHGlobal` 的 HRESULT 未检查；成功路径上 `IStream` 从不释放。

`Job.cs:49-59,78-86`：`Marshal.AllocHGlobal` 的扩展信息块在任何路径都不释放；`SetInformationJobObject` 失败时 `throw` 还会泄漏 job 句柄（该类型没有终结器）；`FilterReader` 从未使用 `Job`。

`FilterReader.cs:253-258,1280-1305`：`Dispose` 无条件 `ReleaseComObject` + `FreeCoTaskMem`，与正在执行的 `Read` 之间没有同步（看门狗线程释放会让原生滤镜写入已释放内存 → 进程级访问冲突）；终结器里做实际释放工作且 `ReleaseComObject` 未包 try/catch，终结器抛异常会**直接终止进程**。

`NativeMethods.cs:676-700`：`VT_BLOB` 用滤镜提供的 `lVal`/`pBlobData` 直接分配与拷贝，`VT_BSTR`/`VT_LPWSTR` 直接解引用原始指针——畸形文档可造成不可捕获的 AV（.NET Core 下 AV 无法 catch，进程直接崩）。

> 说明：子代理已核实接口 GUID、vtable 顺序、`STAT_CHUNK`/`PROPVARIANT`/`PROPERTYKEY` 结构布局、SDK 枚举值与健康路径的缓冲区运算**均正确**，问题集中在错误路径与生命周期管理。

### P1-4 `IFilterPlugin` 接管了所有文件，且会静默压制默认文本搜索

`src/IcedAstroGrep.App/Plugins/FileHandlers/IFilterPlugin.cs:135` + `PluginManager.cs:156-158`

> **复核更正**：初版评审称该插件默认**启用**，这是误读。`PluginManager.cs:157` 传入的是
> `new PluginWrapper(iFilterPlugin, string.Empty, iFilterPlugin.Name, true, false, 2)`，而
> `PluginWrapper` 的构造函数签名为 `(plugin, assemblyPath, assemblyName, internalPlugin, enabled, index)`
> （`PluginWrapper.cs:64`），即 `internalPlugin: true, enabled: **false**`。该插件本就是默认关闭的，
> "先于默认搜索处理每一个文件"只在用户手动启用后发生。

- `IsFileSupported` 恒为 true，因此一旦启用，"File Handlers"插件会先于默认文本搜索处理**每一个**文件；`Grep.cs:1010-1013` 在 `IsFileSkipped == false` 时直接 `return`，默认的流式搜索不再执行。
- iFilter 失败（截断、`IFFileTooLarge`、超时、格式错误等）时只把异常上报、却不置 `IsFileSkipped`，文件既没被 iFilter 搜到、也不会走默认搜索，结果是**静默漏报**（false negative）——对一个搜索工具来说是最坏的失败模式。
- `IFilterPlugin.cs:81` 的 `Extensions` 返回 `"File Handlers"`（不是扩展名列表），该字符串会被当作"扩展名"列显示在插件管理界面。
- `IFilterPlugin.cs:135` 使用默认 `FilterReaderOptions`（`NoTimeout`），把 §P1-2 的死循环风险引入主搜索路径。
- `FilterSearcher.cs:88-91`：`ignoreCase` 时只对**行文本**做 `ToUpperInvariant()`，搜索词没有同步大写 → 任何非大写搜索词都返回 false。

修复建议：默认关闭该插件；把 iFilter 失败明确定义为"回退到默认搜索"并上报；显式设置读取超时；修正 `Extensions` 与 `FilterSearcher` 的大小写处理。

### P1-5 `FilterItem` 序列化会因值含 `|` 而整体失效

`src/IcedAstroGrep.Core/Filtering/FilterItem.cs:199-212`

```csharp
string[] values = value.Split(DELIMETER);   // DELIMETER = '|'
...
item.Value = values[1];                     // 值里含 '|' → 索引错位
item.Enabled = Convert.ToBoolean(values[5]); // 字段不足 → IndexOutOfRangeException
```

`ToString()`（第 409-412 行）不做任何转义。用户只要在排除项里写一个含 `|` 的值（正则、路径片段、多扩展名组合），`FromString` 就会抛异常；`SettingsIO.Load` 会捕获并记录，但**整份排除项配置被静默丢弃**，用户下次启动会发现设置"莫名其妙丢了"。修复：序列化时转义分隔符，或改用 XML/JSON 存 `FilterItems`。

### P1-6 启动期写盘失败会让程序直接无法启动

`src/IcedAstroGrep.App/Plugins/PDF/PDFPlugin.cs:51-60, 321-325`

```csharp
public PDFPlugin()
{
    if (string.IsNullOrEmpty(pdfToTxtAppPath)) ExtractPDFToTxtApp();   // File.WriteAllBytes，无 try/catch
    IsAvailable = !string.IsNullOrEmpty(pdfToTxtAppPath);
}
```

构造链：`frmMain` 构造函数 → `PluginManager.Load()`（`frmMain.cs:1254`）→ `new PDFPlugin()` → `ExtractPDFToTxtApp()` → `Directory.CreateDirectory` + `File.WriteAllBytes`。

- 任何一次失败（`%TEMP%\IcedAstroGrep-PDF\pdftotext.exe` 被上次残留进程/杀软/权限锁住、磁盘满、重定向的 TEMP 不可写）都会让构造抛出并冒泡出 `frmMain` 构造函数。
- `Program.cs:78-83` 的 `Application.ThreadException` 只在**消息循环内**生效，而异常发生在 `Application.Run` 之前 → 走默认的未处理异常路径，进程终止，用户看到的是"程序一启动就崩"。
- 顺带：每次启动都无条件重写 1 MB 的 `pdftotext.exe`，属无谓 IO。

修复建议：`try/catch` + `IsAvailable = false` + 记录日志；仅在文件缺失或哈希变化时提取。

### P1-7 `pdftotext` 子进程无超时、无取消、无回收

`PDFPlugin.cs:340-350`

`process.WaitForExit()` 无超时；`Grep.Abort()` 只在文件之间生效，因此"取消"对卡住的 PDF 无效；应用退出时子进程成为孤儿；`Unload()`（第 311 行）是空实现，临时 `.txt` 文件永久堆积在 `%TEMP%\IcedAstroGrep-PDF`。

另外 `PDFPlugin.cs:338` 用 `Path.GetFileNameWithoutExtension` 生成输出名，**不同目录下的同名 PDF 会共用同一个输出文件**——先跑的残留结果可能被当成另一个文件的内容返回。

修复建议：`WaitForExit(timeout)` + `Kill(entireProcessTree: true)`；取消令牌联动；`finally` 删除临时文件；输出名加唯一后缀（进程 id / 哈希）。

### P1-8 插件把整个文档读进内存，可 OOM

- `MicrosoftExcelPlugin.cs:340-386`：所有工作表 → `StringBuilder` → `Split` → 每行的 `List`。
- `MicrosoftWordOpenXMLPlugin.cs:336-354`：`StringBuilder` + `Split`。
- `PDFPlugin.cs:356`：`File.ReadAllLines`。
- 对比：内置搜索路径是逐行流式的（`Grep.SearchFileContents`），内存占用恒定。

一个几百 MB 的 xlsx/docx 就能把进程打爆。修复：改为逐行/逐块产出；或设定文档大小上限并明确告知用户。

### P1-9 便携承诺与注册表行为矛盾

- `Program.cs:96` → `Legacy.ConvertLanguageValue()` → 读写 **HKCU** 注册表。
- `frmMain.cs:1235,1240,1251` → `Legacy.ConvertGeneralSettings/ConvertSearchSettings/DeleteRegistry`：启动时把 `HKCU\Software\VB and VBA Program Settings\IcedAstroGrep` 下的历史设置迁移过来，然后**删除整个键树**。

这是一个"绿色便携"工具却在未告知用户的情况下访问并删除注册表项。虽然只针对本应用自己的旧键，仍然值得处理：要么移除（分支本来就是全新项目，不存在需要迁移的用户），要么改为显式询问。

### P1-10 静默吞异常

- `Convertors.cs:343-358` `InvokeIfRequired`：`catch { }` 吞掉一切。表单关闭期间 UI 编组失败会被完全掩盖，排查问题极其困难。
- `Grep.cs:709-712`：递归子目录的 `catch { }` 会吞掉 `StackOverflowException` 之外的任何错误，包括本应上报的 `SearchError`。
- `FilterItem.cs:253-256` `IsBinaryFile`、`Registry.cs` 多处：`catch { return false; }` 把"读不了"当成"不是二进制/没有设置"。
- 综合效果：与 §P1-4 的静默漏报叠加，用户无法区分"没搜到"和"没能搜"。

---

## 4. 次要问题（P2）

**引擎（Core）**

1. `Grep.cs:824` 的 `_context` 数组按 `SearchSpec.ContextLines + 1` 分配，`ContextLines` 无上限；配置文件里写一个大数即可造成内存放大。
2. `Grep.cs:827` 局部变量 `userFilterCount` 是死代码（始终为 0，真正生效的是同名字段），容易误导后续维护者。
3. `Grep.cs:82` 构造函数对 `searchSpec` 本身不做 null 检查，只检查 `searchSpec.EncodingDetectionOptions`（`Utils.cs:1-5` 未使用的 using；`Grep.cs:3` `System.Linq` 使用正常）。
4. 递归遍历无符号链接/联接点环检测（`Execute` 第 699-714 行），目录联接成环时栈溢出。
5. `FilterItem.cs:234` `readStream.Read` 单次读取，且只扫描 1 KB，与注释所述"前 10 KB"不符；`CheckLongAgainstOption`（第 471 行）对非法 `Value` 直接 `Convert.ToInt64`，抛 `FormatException`。
6. 无最大结果数限制，宽泛搜索（如单字符正则）会把命中全量存在 `List<MatchResult>` 中。
7. 预留的死代码：`EncodingTools.cs`（`DetectOutgoingStreamEncoding`、`IsAscii`、`OpenTextFile`、`ReadTextFile` 等）、`AutoItEncodingDetector.GetBomLengthFromEncodingMode`、`Registry.SaveRegistrySetting`、`CharsetProber.SetOption` 无任何引用——第三次提交声称"清理已无入口的死代码"，实际仍有残留。

**应用（App）**

8. `ApplicationPaths.cs:21` 把所有数据（设置、日志、缓存）放在 exe 同目录。在 `Program Files` 或只读介质下运行会写入失败，且 `SettingsIO.Save`/`LogClient` 只记日志、不提示用户——设置静默不保存。
9. `SettingsIO.cs:141-204` 非原子写入（直接 `XmlDocument.Save` 覆盖），崩溃/断电可能损坏配置；`Load` 又对任何解析异常整体放弃，等于配置全丢。
10. `TextEditors.LaunchEditor`（`TextEditors.cs:292-302`）把文件名/搜索词直接拼进命令行字符串；编辑器是用户自己配置的，属自伤面，但应在文档中说明，或改用 `ArgumentList`。
11. `PluginManager.cs:191-192` 对插件配置做裸 `bool.Parse`/`int.Parse`，该路径在 `frmMain` 构造函数中执行 → 配置文件损坏 = 应用无法启动。
12. `PluginManager.cs:280` 用 `Assembly.LoadFrom` 加载 exe 同目录 `Plugins\*.dll` 且默认启用，无签名/白名单/确认。这些插件与主进程同权限同地址空间，能写入该目录的人即获得代码执行。
13. `MicrosoftWordPlugin.cs:252-305`：`CloseDocument`/`ReleaseSelection` 只在成功路径，异常时留下隐藏的 WINWORD 进程持有文档（且文档以读写方式打开）。`ListManager.cs:567-581` 按文档里的缩进值生成空格字符串，`Left="2000000000"` 即可造成约 8.9M 字符/段落的分配。
14. `MediaTagsPlugin.cs:190`：`TagLib.File`（`IDisposable`，持有 `FileStream`）从不释放，每个媒体文件泄漏一个句柄/锁。
15. `IcedAstroGrep.App.csproj` 引入 `UseWPF`（AvalonEdit 需要），但 `Core` 也带 `UseWindowsForms`——依赖方向虽正确，Core 对 UI 框架的依赖值得在 README 的"UI-free"表述上修正。
16. 提交历史只有 3 个 commit，且第三次提交信息声称清理死代码；建议后续按主题拆分提交，便于追溯。

**许可与合规**

17. `LICENSE`（GPL-2.0）+ `NOTICE` 结构正确，但 `src/IcedAstroGrep.App/Resources/pdftotext.exe` 是**第三方二进制**（Xpdf / Glyph & Cog 的 `pdftotext`），`NOTICE` 未提及；Xpdf 为 GPL-2 且在商用/再分发上另有约束，需要补充来源、版本与许可说明。
18. `tools/import-upstream.ps1` 硬编码了作者本机的绝对路径，作为仓库内工具无法复用；README 未提及它，建议删除或参数化。

**测试**

19. 仅 4 个测试（字面量/大小写/整词/正则的基本命中），未覆盖：否定匹配、上下文行、仅文件名、命中数过滤、排除项、编码检测与缓存、插件路径、命令行导出。§2/§3 中的每个缺陷都对应一个缺失的测试。
20. `GrepTests` 依赖 `Environment.NewLine` 与临时目录，本身可接受；建议补 `Grep` 直接对 `StartFilePaths` 的错误路径、不可读文件的用例。

---

## 5. 环境相关观察（非代码缺陷，但会影响协作者）

1. 默认多节点 `dotnet build IcedAstroGrep.slnx` 在**沙箱内**失败且不打印任何错误（`0 Error(s)` + exit 1）。**（复核更正）** 初版评审把原因归为 `_GetProjectReferenceTargetFrameworkProperties` 的子 MSBuild 任务失败、与 SDK 目录下缺失的 `Microsoft.NET.SDK.WorkloadAutoImportPropsLocator` 有关，这是误判：在关闭沙箱后同一条命令 `Build succeeded`（exit 0，0 error）。真实原因是沙箱的 ACL 受限令牌禁止打开命名管道，而 MSBuild 多节点正是靠命名管道通信。因此这与本机 SDK 安装无关，也不是代码问题；加 `-m:1 -nodeReuse:false` 可绕开。已在 README 记录。
2. `dotnet test` 在沙箱下必然失败：vstest 测试宿主调用 `Process.EnableRaisingEvents` 时被拒绝（`Win32Exception (5): Access is denied`）。机理同上——受限令牌下连对自身进程调 `OpenProcess` 都被拒，而宿主需要查询其父进程。**关闭沙箱后 `dotnet test` 正常**（`Failed: 0, Passed: 26, Total: 26`）。沙箱内则改用自建 harness 反射执行测试方法。

---

## 6. 建议的修复优先级

| 顺序 | 事项 | 理由 |
|---|---|---|
| 1 | 给所有 `Regex` 加 `matchTimeout`，并把每次搜索的正则编译提到循环外 | 唯一能"永久卡死并让取消失效"的缺陷，已复现 |
| 2 | 修 `EncodingCache.RemoveItem` + 加锁；`StartSearch` 先 `Abort` 并等待旧搜索 | 数据结构不一致 + 并发写全局状态，会产生难以复现的错乱 |
| 3 | `FilterItem` 序列化转义（或改用 JSON/XML） | 用户配置静默丢失 |
| 4 | `PDFPlugin` 构造函数加 try/catch；`pdftotext` 加超时/回收/唯一输出名 | 直接导致"程序无法启动"和"搜索卡死" |
| 5 | 修 `FilterReader.Read()`；为 iFilter 循环补 `default` 与"零进展"终止；默认启用超时 | 公开 API 必然抛异常 + 死循环 + 原生内存误用 |
| 6 | "File Handlers" 默认关闭，iFilter 失败改为回退并上报 | 消除静默漏报 |
| 7 | 插件的整文档读入改为流式或加大小上限 | OOM |
| 8 | 处理便携目录不可写、`SettingsIO` 原子写入 | 实际部署中的第一类故障 |
| 9 | 补测试与 CI（至少覆盖 `Grep` 的过滤/否定/上下文/命中数路径与 `FilterItem` 往返序列化） | 上述多数缺陷都靠测试即可拦住 |
| 10 | 补 `pdftotext` 的第三方许可说明；清理 `Legacy` 注册表代码与残留死代码 | 合规与长期可维护性 |

### 修复状态（同上表顺序）

| 顺序 | 状态 | 说明 |
|---|---|---|
| 1 | 已修复 | `Grep.SearchRegExTimeout`（2 秒）应用于全部搜索用 `Regex`；超时转换为 `SearchRegexTimeoutException` 上报并中止搜索；正则改为每次搜索编译一次（`Grep.Execute` 中构建一次）；`frmMain` 输入校验增加探针匹配。注意 `Regex.Matches()` 是惰性求值的，超时实际在枚举集合时才抛出，故求值被显式提前到 `Grep.EvaluateAllMatches`。另：单文件内取消检查改为每 1024 行一次。 |
| 2 | 已修复 | `EncodingCache.RemoveItem` 补上字典移除；内部加锁 + 线程安全单例；淘汰改为基于条目数自洽、LRU 更新/删除 O(1)；新增 `Grep.AbortAndWait`，`frmMain.StartSearch` 在开新搜索前中止并等待旧搜索线程退出。 |
| 3 | 已修复 | `FilterItem` 的字段分隔符 `|` 与列表分隔符 `<` 现在会被转义；新写入的条目带 `~v2~` 前缀，`FromString` 据此区分新格式与旧格式——旧配置（不含前缀）完全不转义，因此其中的反斜杠（UNC 路径、正则）不会被误当转义符，向后兼容。同时补上 `tests/IcedAstroGrep.Core.Tests/FilterItemTests.cs`（22 个用例）。 |
| 4 | 已修复 | `PDFPlugin` 构造函数不再抛出：提取失败只记录日志并标记为不可用；内容一致时不再重写约 1 MB 的 `pdftotext.exe`。`pdftotext` 增加 60 秒超时 + `Kill(entireProcessTree: true)`，输出名加入全路径哈希，输出文件在 `finally` 删除，`Unload` 清理过期残留。另修正由终结器调用、会删除共享临时目录的 `Dispose()`。**取消令牌未接入**：插件契约 `IIcedAstroGrepPlugin.Grep` 没有令牌参数，接入需改接口（影响所有插件），超出本项范围；超时已把取消延迟限制在 60 秒内。 |
| 5 | 已修复（P1-3 部分残留） | `Read()` 改用 1 长度缓冲区；`_chunk.flags` 补 `default`（`CHUNKSTATE` 是 `[Flags]`，组合值合法）、零长度文本推进、新增"零进展"守卫（连续 64 次无字符且无新块 → 抛 `IFFilterPartiallyFiltered`）；`FilterReaderOptions` 默认启用 `TimeoutWithException`（60 秒）；`CHUNK_VALUE` 去掉会泄漏的 `AllocHGlobal`、改用 `PropVariantClear` + `FreeCoTaskMem`；`Dispose` 幂等化并加 `IsComObject` 判断，终结器不再抛异常。**残留**：`FilterLoader` 的 IStream 释放 / 单次 Read / HRESULT 检查、`Job.cs` 泄漏、`VT_BLOB`/`VT_BSTR` 的 AV 路径。 |
| 6 | 已修复（"默认关闭"一项经复核本就满足） | iFilter 任何失败现在既上报错误、又置 `IsFileSkipped` 让默认文本搜索继续处理该文件（原先只上报不回退 = 漏报）；读取超时显式设为 60 秒；`Extensions` 不再返回插件名。`FilterSearcher.FileContainsText` 改为按 `ignoreCase` 用 `StringComparison` 比较（原先只大写行文本，非大写搜索词永远匹配不到）。"默认关闭"经复核**本就满足**：`PluginManager.cs:157` 传的是 `enabled: false`。 |
| 7 | 已修复 | Excel 与 PDF 改为逐行产出：Excel 按行消费 `ExcelDataReader` 的前向读取器（不再逐表拼大字符串 → 切分 → 拷入列表），PDF 用 `StreamReader` 逐行读取（不再 `File.ReadAllLines`）且临时输出在枚举结束时删除。实测同一份 30 万行 xlsx：托管堆峰值 **295 MB → 11 MB**，提取结果逐行一致。Word 因 OpenXML SDK 必须把整份 `word/document.xml` 物化为对象树，改为对主文档部件**解压后**大小设 32 MB 上限并显式报错（值取自 zip 中央目录，无需解压）。 |
| 8 | 已修复 | `SettingsIO.Save` 改为"完整写入 `.tmp` + `Flush(true)` + 原子替换"，旧文件保留为 `.bak`；`Load` 在主文件损坏时回退到 `.bak`（原先一次解析失败 = 设置全丢）。新增 `ApplicationPaths.IsDirectoryWritable`，启动时探测程序目录并在不可写时记录错误 + 弹出明确提示（`ApplicationFolderNotWritable` 语言键），不再让保存静默失败。**刻意不回退到 `%APPDATA%`**：便携承诺是数据在程序旁边。**残留**：会话中途才出现的写入失败仍只进日志（用户可在日志窗口看到），未做即时的界面告警。 |
| 9 | 已修复 | 测试由 26 增至 **46**：新增 `GrepSearchTests`（否定 / 上下文行 / 仅文件名 / 最低命中数 / 扩展名·文件名·目录排除 / 子目录递归）、`GrepRegexTimeoutTests`（超时同步抛错与异步上报）、`GrepAbortTests`、`EncodingCacheTests`（移除 / 淘汰一致性 / 并发）、`PluginContractTests`（失败但跳过 → 上报 + 回退；声称已处理 → 压制默认搜索）。关闭 xUnit 并行（`Grep` 与 `EncodingCache` 有进程级状态）。新增 `.github/workflows/ci.yml`（`windows-latest` 上 restore/build/test，Release）。 |
| 10 | 已修复 | 删除 `Legacy.cs`（612 行）与 `Registry.cs`（355 行）及其 5 个调用点，应用不再读写 / 删除 HKCU（iFilter 只读 HKLM 的查找保留）。`EncodingTools` 移除 7 个无引用成员（其中 `ReadTextFile` 本身还是坏的）、`AutoItEncodingDetector.GetBomLengthFromEncodingMode`、`CharsetProber.SetOption` 一并移除；**未**对 vendored 目录做逐成员审计（`PreferedEncodingsForStream` 只写不读，牵动静态构造逻辑，未动）。`tools/import-upstream.ps1` 参数化。`pdftotext` 许可补齐：核实为 Xpdf **4.01.01**，新增 `third-party/xpdf/`（许可说明 + 从二进制抓取的帮助文本）并随构建复制，README 提示发布时不得删除。 |

> 修复过程中**新发现**的三个缺陷（原评审未提及，均已修复）：
> 1. **Excel 插件在本分支上完全不可用**——`ExcelDataReader` 的配置构造函数解析回退代码页 1252，而 .NET Core 默认不注册旧代码页，每个 .xls/.xlsx 都抛 `NotSupportedException`。新增 `LegacyEncodingSupport.EnsureRegistered()`（注册 `CodePagesEncodingProvider`）；该 provider 由 WindowsDesktop 框架提供，无需显式包引用（显式引用会触发 NU1510），已由 `LegacyEncodingSupportTests` 断言守住。这同时修好了编码检测里 `Encoding.GetEncoding(codePage)` 对旧代码页的失败。
> 2. **Word 插件在无 styles 部件的文档上 NRE**（`StyleDefinitionsPart` 直接解引用），已改为空安全。
> 3. **`FilterSearcher.FileContainsText`（P1-4）之外**：`dotnet test` 与多节点 `dotnet build` 在沙箱下的失败均源于 ACL 受限令牌（进程句柄 / 命名管道），关闭沙箱后分别通过 `26/26` 与 `Build succeeded`——见 §5 的复核更正。

> 验证结果：`dotnet build -m:1 -nodeReuse:false` 成功（0 warning / 0 error）。
> **本节此前记录的"沙箱下 `dotnet test` 必然失败"已不适用**：会话权限切换为 `danger-full-access` 后，`dotnet test` 直接通过（`Failed: 0, Passed: 26`），多节点 `dotnet build` 也成功。失败根因是受限令牌，不是代码（见 §5 复核更正）。
> 按轮次记录的验证：
> - P0 轮次：4 个既有测试 + 9 项新增检查（正则超时同步中止 / 异步上报、正则单次编译、缓存移除 / 淘汰 / 8 线程并发、`AbortAndWait` 空闲与运行中）共 13 项通过；
> - P1-5 轮次：`FilterItemTests` 22 个用例 + `GrepTests` 4 个用例共 26 项通过（`FilterItemTests` 已入库，可在能跑 `dotnet test` 的环境中直接执行）；
> - P1-6/P1-7 轮次：`PDFPlugin` 6 项检查通过——含真实超时路径（60.0 秒触发、假转换器的孙进程心跳在 8.7 秒后停止，证明整棵进程树被回收）。
> - P1-1/P1-2/P1-3 轮次：把 IFilter 源码编进 harness，用实现内部 `IFilter` 接口的**脚本化假滤镜**驱动 `Read()`，12 项检查通过——含 `Read()` 返回首字符、真实文件端到端读取、未知 flags/零长度文本/持续 GetChunk 失败三条死循环路径均在 0 ms 内以异常收敛、默认超时确实触发、2000 次 CHUNK_VALUE 读取与释放无堆损坏。
>   **该轮次抓到并修正了一个自引入的严重缺陷**：给 GetChunk 结果 switch 添加 `default: _chunkValid = false;` 会把正常的 `S_OK` 也判为无效，使整条 iFilter 路径读不出任何内容（正是 §P1-4 所警告的静默假阴性）。因第 516 行已把 `_chunkValid` 赋为 `result == S_OK`，`default` 分支不应再改动它。
> - P1-4 轮次：9 项检查通过——`FilterSearcher` 的大写/混合大小写/大小写敏感/null 词、`Extensions` 不再冒充扩展名列表、无 iFilter 的文件交还默认搜索、以及两项端到端：①「插件报错 + `IsFileSkipped`」确实同时产生"上报错误"与"默认搜索命中"（这正是本项修复所依赖的契约）；②启用 File Handlers 后真实系统 iFilter 命中且 `FromPlugin == true`（若沿用第 5 项那个自引入缺陷，此项会失败）。
> 临时 harness 未入库；`PDFPlugin` 与 `FilterReader` 的检查因分别位于 App 项目、需要假 COM 滤镜，未固化为仓库测试。
> - P1-8 轮次：以"改动前先记录提取结果、改动后逐行比对"的方式验证——Excel（多工作表、内嵌制表符、含 `|`/`<` 的单元格、每表末尾空行）与 Word（段落、空段落、表格行）的提取结果 17 行**完全一致**；内存峰值以同一份 30 万行 xlsx 前后对比（295 MB → 11 MB）；PDF 用手写 xref 的最小 PDF 首次做到端到端（命中正确，且全量扫描与提前 `break` 两条路径都无临时文件残留）；Word 上限以 40 MB 部件（590 KB 包）验证拒绝并给出明确信息。
> - §6-9 轮次：权限解除后 **`dotnet test` 直接可用**，新增测试后 **46/46 通过**。为避免"测试通过但抓不到回归"，做了一次抽查：临时把 `EncodingCache.RemoveItem` 还原成修复前写法，`RemoveItem_RemovesTheEntryFromTheCache` 随即失败（`Assert.False()` Failure），恢复后重新通过。CI 使用的三条 Release 命令（`dotnet restore` / `build --no-restore -c Release` / `test --no-build -c Release`）已在本地逐一验证通过；GitHub Actions 本身无法在本地运行，因此 runner 镜像与 `10.0.x` SDK 的可用性未经验证。
> - §6-8 轮次：新增 App 侧测试项目后共 **56/56 通过**（App.Tests 7 + Core.Tests 49）。同样做了抽查：临时禁用 `SettingsIO.Load` 的备份恢复分支后，`Load_RecoversFromTheBackupWhenTheDocumentIsCorrupt` 失败，恢复后重新通过。
