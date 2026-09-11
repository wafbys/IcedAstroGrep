# 变更记录

本文件记录 IcedAstroGrep 的主要变更。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

上游基线：[AstroGrep](http://astrogrep.sourceforge.net) 4.4.9（GPL-2.0-or-later）。

## [未发布]

### 修复

- **P0-1 正则表达式可导致搜索线程永久挂死**：所有搜索用 `Regex` 现在都带匹配超时（`Grep.SearchRegExTimeout`，2 秒）；超时被转换为 `SearchRegexTimeoutException` 并以可见的搜索错误上报并中止本次搜索，不再让线程无限期卡死。`FilterSearcher`、`HTMLHelper` 中的正则同样加了超时。搜索用的正则表达式改为每次搜索编译一次（原先是每个文件重新构造，配合 `RegexOptions.Compiled` 等于每个文件做一次动态 IL 编译）。搜索输入校验新增探针匹配，能在开始搜索前就拒绝会触发灾难性回溯的模式。单文件内的取消检查由"每行"放宽为"每 1024 行"一次，配合超时使"取消"按钮真正生效。
- **P0-2 `EncodingCache` 数据结构不一致且无同步**：`RemoveItem` 现在同时从字典中移除条目（原先只移除 LRU 链表节点，导致过期条目被写回磁盘、淘汰逻辑走在不匹配的 LRU 上）；缓存内部加锁并使用线程安全单例；淘汰改为基于实际条目数的自洽实现，LRU 的更新与删除改为 O(1) 且不会与字典内容脱节；`Save` 改为在锁内取快照后再写盘。
- **P0-3 允许同时运行两个搜索**：开始新搜索前先中止旧搜索并等待其线程退出（新增 `Grep.AbortAndWait`），避免两次搜索并发争用插件实例与编码缓存文件。等待前先解绑事件，因此不会与 UI 线程互锁。
- **P1-5 `FilterItem` 序列化遇到 `|` 整体失效**：排除项的字段分隔符 `|` 与列表分隔符 `<` 现在会被转义，含这些字符的值（正则、路径片段、多扩展名组合）可以正常保存与读取，不再抛异常并导致整份排除项配置被静默丢弃。新写入的条目带 `~v2~` 格式前缀；旧配置按原规则解析，其中的反斜杠（UNC 路径、正则）不会被误当作转义符。
- **P1-6 `PDFPlugin` 启动期写盘失败导致程序无法启动**：`pdftotext.exe` 的提取改为在 `try/catch` 中进行并记录日志——临时目录被锁、磁盘满、`TEMP` 不可写等任何失败都只会让插件标记为不可用，不再从 `frmMain` 构造函数冒泡出去终止进程。提取前会与内嵌副本比对内容，一致时不再每次启动重写约 1 MB 的二进制。
- **P1-7 `pdftotext` 子进程无超时、无回收**：子进程增加 60 秒超时，超时后 `Kill(entireProcessTree: true)` 回收整棵进程树；输出文件名加入源文件全路径哈希，不同目录下的同名 PDF 不再共用输出文件（避免读到残留结果）；转换输出在 `finally` 中删除，`Unload` 清理崩溃残留的过期输出。另修正 `Dispose()`：它原先由终结器调用且会删除整个共享临时目录（含内嵌工具与其他实例正在读取的输出），可能破坏进行中的搜索。取消令牌未接入——插件契约没有令牌参数，需改接口，超出本项范围；超时已把取消延迟限制在 60 秒内。
- **P1-1 `IFilterTextReader.Read()` 必然抛异常**：该重载用 0 长度字符缓冲区调用三参数重载，每次都会抛 `ArgumentException`（本应返回下一个字符）。改为 1 长度缓冲区。
- **P1-2 iFilter 读取循环可 100% CPU 空转**：`CHUNKSTATE` 是 `[Flags]` 枚举，组合值合法，原先 `switch (_chunk.flags)` 无 `default`，一个 case 都不命中且块保持有效，会在纯托管代码里无限空转；`S_OK` 但零长度文本且无分隔符时也会重复读取同一个块。现已补上 `default` 分支、让零长度文本推进到下一块，并增加"零进展"守卫——连续 64 次既无字符产出也无新块时以 `IFFilterPartiallyFiltered` 显式报错，而不是死循环或静默截断。读取超时默认启用（`TimeoutWithException`，60 秒/文档；显式设 `NoTimeout` 可退出），选择"抛异常"而非"静默当读完"是为了不产生假阴性。
- **P1-3（部分）原生内存误用**：`CHUNK_VALUE` 路径不再用 `Marshal.AllocHGlobal` 预分配一块会被滤镜指针覆盖的内存（每个值块泄漏约 24 字节），也不再用 `FreeHGlobal` 释放滤镜以 `CoTaskMemAlloc` 分配的内存；改为读取后对该 PROPVARIANT 恰好调用一次 `PropVariantClear`，再用 `FreeCoTaskMem` 释放变体本身。`Dispose` 增加幂等保护与 `IsComObject` 判断，`ReleaseComObject` 不再从终结器抛出（终结器抛异常会直接终止进程）。
- **尚未处理（P1-3 剩余）**：`FilterLoader` 的 `IStream` 从不释放、单次 `Read` 不保证填满、`CreateStreamOnHGlobal` 的 HRESULT 未检查；`Job.cs` 的句柄与内存泄漏；`VT_BLOB`/`VT_BSTR` 对畸形文档的可致 AV 路径。
- **P1-4 iFilter 失败导致静默漏报**：`FileHandlersPlugin` 在任何 iFilter 失败（读取超时、内容截断、文件过大、格式错误、口令保护等）时，现在既上报搜索错误、又置 `IsFileSkipped` 让默认文本搜索继续处理该文件。原先只上报错误却不回退，文件既没被 iFilter 搜到、也不会走默认搜索，等于漏报。读取超时改为显式设置（60 秒），不再依赖库默认值。`Extensions` 不再返回插件名 `"File Handlers"`——它在插件列表里被当作"扩展名"列显示，改为说明该插件依赖系统 iFilter。
- **P1-4 `FilterSearcher.FileContainsText` 大小写错误**：原先只把行文本 `ToUpperInvariant()` 而搜索词没有同步大写，导致任何非大写搜索词都永远匹配不到；改为按 `ignoreCase` 使用 `StringComparison` 比较，并忽略 `null` 搜索词。
- **P1-4（复核更正）**：评审称 "File Handlers" 插件"默认**启用**"有误。`PluginManager.cs:157` 传给 `PluginWrapper` 的参数是 `internalPlugin: true, enabled: false`（构造函数签名见 `PluginWrapper.cs:64`），该插件本就是**默认关闭**的；"先于默认搜索处理每一个文件"只发生在用户手动启用之后。
- **P1-8 插件把整份文档读入内存**：Excel 与 PDF 插件改为逐行产出——Excel 直接按行消费 `ExcelDataReader` 的前向读取器（不再为每个工作表拼一个大字符串、再切分成数组、再拷进列表），PDF 转换结果用 `StreamReader` 逐行读取（不再 `File.ReadAllLines`），且临时输出在枚举结束时删除（含提前 `break` 的情形）。实测同一份 30 万行 × 120 字符的 xlsx：旧实现托管堆峰值 **295 MB**，新实现 **11 MB**，提取结果逐行完全一致。Word 插件因 OpenXML SDK 会把整份 `word/document.xml` 物化为对象树、无法在不改用前向 XML 读取器的前提下流式化，改为对**主文档部件解压后大小**设 32 MB 上限并显式报错（该值直接取自 zip 中央目录，无需解压）。一个 590 KB 的包解压后是 40 MB 的 document.xml，正说明这道限制的必要性。
- **新发现：Excel 插件在 .NET 10 上完全不可用**：`ExcelDataReader` 的配置构造函数会解析回退代码页 1252，而 .NET Core 默认不注册旧代码页，于是每个 .xls/.xlsx 都抛 `NotSupportedException`。新增 `LegacyEncodingSupport.EnsureRegistered()`（注册 `CodePagesEncodingProvider`，在应用启动与 Excel 插件内各调用一次）并引入 `System.Text.Encoding.CodePages` 包。这同时修好了编码检测路径中 `Encoding.GetEncoding(codePage)` 对旧代码页的失败。
- **新发现：Word 插件在没有 styles 部件的文档上 `NullReferenceException`**：`StyleDefinitionsPart` 为 null 时直接解引用。已改为空安全（无样式部件是合法文档）。
- **新发现（评审更正）**：评审 §5-1 将多节点 `dotnet build` 失败归因于 SDK 缺文件有误，实为沙箱 ACL 受限令牌禁止命名管道；关闭沙箱后同一条命令 `Build succeeded`。`dotnet test` 同理（vstest 测试宿主需要 `OpenProcess` 父进程权限），关闭沙箱后 26 个测试全部通过。

### 测试

- 新增 `tests/IcedAstroGrep.Core.Tests/FilterItemTests.cs`：覆盖排除项序列化的往返（含 `|`、`<`、反斜杠、UNC 路径、空值、大小显示选项）、旧格式配置的兼容解析、排除项日志详情的拆分。

### 已知问题

- 评审记录中尚未处理的 P1/P2 项：`FilterLoader` 的 IStream 释放与 HRESULT 检查、`Job.cs` 的资源泄漏、`VT_BLOB`/`VT_BSTR` 的畸形文档 AV 路径、插件把整份文档读入内存、`SettingsIO` 非原子写入等。

### 计划中

- 补充测试覆盖：否定匹配、上下文行、仅文件名、命中数过滤、排除项、编码检测与缓存、插件路径、命令行导出。
- 持续集成（GitHub Actions）构建与测试流水线。

## [1.0.0] - 2026-09-10

首个版本。基于 AstroGrep 4.4.9 重建为 .NET 10 便携版。

### 新增

- 便携（绿色）发布形态：设置、日志、编码缓存均存放在 `IcedAstroGrep.exe` 同级目录，无安装器、无资源管理器右键集成。
- 面向 .NET 10 的 SDK 风格项目结构（`net10.0-windows`、`PackageReference`），解决方案文件 `IcedAstroGrep.slnx`。
- 仅浅色主题的 WinForms 界面。
- Core 引擎保持与 UI 解耦，便于后续接入其它界面宿主（如 WinUI 3）。
- `tools/import-upstream.ps1`：从 AstroGrep 源码树导入并批量重命名命名空间与路径的辅助脚本。

### 变更

- 目标框架由 .NET Framework 迁移至 `net10.0-windows`。
- 搜索取消改为协作式取消（`CancellationToken`），替代 `Thread.Abort`。
- 编码缓存持久化改用 JSON + Deflate，替代 `BinaryFormatter`。
- 统一日志到 NLog，日志与归档文件写入程序目录的 `Log` 子目录。
- 项目拆分为 `IcedAstroGrep.Core`（搜索引擎与插件契约）、`IcedAstroGrep.IFilter`（Windows IFilter 封装）、`IcedAstroGrep.App`（WinForms 界面）与 `IcedAstroGrep.Core.Tests`。

### 移除

- 移除深色主题及其颜色表、渲染器（仅保留浅色主题）。
- 移除安装器检测逻辑，以及仅用于提权的 `UACHelper`。
- 移除资源管理器右键菜单集成。
- 移除"检查更新"对话框（`frmCheckForUpdate` 及相关窗体）。
- 移除没有对应地址的帮助菜单项、无效的主题选择项、关于页主页链接。
- 移除死代码：`ThemeProvider`、`RegistryMonitor`，以及 `GeneralSettings`、`Shortcuts`、`Win32` 中已无调用方的成员。

### 说明

- 本版本对上游代码有较大幅度的删减与重写，累计移除超过 1700 行不再适用的代码。
- 分发时需一并遵守上游及第三方组件（如 Xpdf `pdftotext`）的许可条款，详见 `NOTICE`。
