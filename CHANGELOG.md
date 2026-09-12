# 变更记录

本文件记录 IcedAstroGrep 的主要变更。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

上游基线：[AstroGrep](http://astrogrep.sourceforge.net) 4.4.9（GPL-2.0-or-later）。

## [未发布]

### 变更

- **项目按"壳"改名，程序集名保持不变**：`IcedAstroGrep.App` → `IcedAstroGrep.WinForms`，为第二个壳（WinUI 3）腾出命名规则。只改项目/目录名，`AssemblyName` 与 `RootNamespace` 仍是 `IcedAstroGrep`：两个壳都产出 `IcedAstroGrep.exe`，`Language.cs` 按"程序集名 + `.Language.` + 区域 + `.xml`"找资源、资源类名是 `IcedAstroGrep.Properties.Resources`，所以这两个名字都不能动。两个壳分开发布，因此同一程序集名不冲突。
- **测试项目按"测谁"分家**：`IcedAstroGrep.App.Tests` → `IcedAstroGrep.WinForms.Tests`（只留壳自身的测试：版本戳、语言资源、壳确实引用 WinForms、HTML 颜色与原 `ColorTranslator` 等价），并新增 **`IcedAstroGrep.AppServices.Tests`**（设置持久化、旧代码页、iFilter 端到端、AppServices 侧的边界断言）。新项目**不设 `UseWindowsForms`**——它跑通本身就证明了这些服务不需要任何 UI 框架，也证明 `CodePagesEncodingProvider` 来自基础框架而不是 WindowsDesktop（这正是"第二个壳只引用 Core + AppServices 就够"的前提）。测试总数不变：84。
- **命令行解析下沉到 `IcedAstroGrep.AppServices`，并删掉一个第三方依赖**：`CommandLineProcessing.cs`（含手写的 `Arguments` 解析器与 `CommandLineArguments` 结构）移入 AppServices（命名空间 `IcedAstroGrep.Windows` → `IcedAstroGrep`，壳按外层命名空间解析，调用点不变），因为两个壳的命令行行为必须一致。同时删除 `CLOptions.cs`：它是**死代码**——真正的解析由 `CommandLineProcessing.Arguments` 手写完成，全仓没有任何地方调用 `Parser.Default.ParseArguments<CLOptions>`，帮助窗口的选项表也是硬编码的，因此 `CommandLineParser` 这个包只服务于它，现已从解决方案中移除（`NOTICE` 同步更新）。这也顺带纠正了 `Arguments` 注释里"支持 `:` 分隔"的错误说法：`:` 作分隔符会破坏含盘符的值（如 `/spath=C:\temp`），实际从未实现，因此改注释而不是改行为。
- **命令行首次获得测试覆盖**（`CommandLineProcessingTests`，11 个用例）：无参数、单目录即起始路径、多参数时必须 `/spath=`、`/stext` 的四种已实现写法、带空格值的去引号、各开关、导出隐含启动搜索且 `/otype` 被小写、上下文行数超限被丢弃、`/?` `/h` `/help`。写这些用例时暴露了两处容易踩的边界（多参数下裸目录被静默忽略、`/otype` 小写），已写进测试而非靠文档口口相传。
- **语言文案下沉到 AppServices，壳只留"套用到控件"的逻辑**：`Language.cs`（1,323 行）按成员块切开——键→文案查找、加载（内嵌 + 外置）、`LanguageItem` 与 7 个 `Language/*.xml` 移入 `IcedAstroGrep.AppServices`（命名空间 `IcedAstroGrep.Windows` → `IcedAstroGrep`），29 个只处理 WinForms 类型的成员留在壳内并改名为 **`WinFormsLocalization`**（`ProcessForm`/`SetControlText`/菜单遍历/`GenerateXml`/`ComboBox` 等，26 处调用点改名）。这样第二个壳能拿到同一套文案，而"怎么套到控件/菜单上"仍归各自的壳。为支撑壳的按名查找，新增两个有文档的访问器：`Language.TextRoot`（已加载的文案文档根，键形如 `screen[@name='frmMain']/control[@name='lblSearchText']`）与 `Language.AvailableLanguages`（内嵌 + 外置语言供选择），`LanguageItem` 转为 public。切分用脚本按成员块进行并带**覆盖校验**（非空行必须全部归属、块不得重叠），中途正是这道校验抓出了 4 处漏项（带空格的泛型字段、私有构造函数、兄弟类 `LanguageItem`、以及容器类自身），避免了静默丢代码。
- **语言文案首次获得测试覆盖**：AppServices 侧断言资源嵌入位置、加载后能取到真实文案、未知键回退到默认值、能按屏幕/控件名查到文案、可选语言含已发布的 7 种；壳侧断言 `WinFormsLocalization.ProcessForm` 真的把 "Search Text" 套到了一个真实 `Form` 的控件上（本地化器此前完全没有测试）。

## [1.2.0] - 2026-09-12

壳无关代码抽取为独立程序集 `IcedAstroGrep.AppServices`，并清掉 1.1.0 §已知问题里的绝大部分残留。WinForms 壳的功能面与 1.1.0 一致：窗口、菜单、搜索结果与导出行为都没有变。

### 新增

- **窗口标题显示版本号与提交哈希**：主窗口标题现在是 `搜索路径 - IcedAstroGrep 1.1.0 (4e264cc)`（无搜索路径时省略路径部分），启动即显示，不依赖是否开始过搜索。"关于"对话框与日志的 STARTING / STOPPING 行同样带上提交哈希，便于凭日志确认用户实际运行的构建。
- **构建期注入提交哈希**：新增 `Directory.Build.targets`，对仓库内每个程序集写入 `AssemblyMetadata("GitHash", <短哈希>)`。哈希取自 `git rev-parse --short=7`，即 GitHub 与 GitHub Desktop 显示的 7 位形式。没有 git 或不在仓库中（例如源码导出）时退化为 `unknown`，不会让构建失败；生成的中间文件只在哈希变化时重写，因此不会破坏增量构建。**注意**：它标记的是构建所基于的提交——在工作区有未提交改动时构建，显示的仍是上一个提交；未加 `-dirty` 标记，因为那会让每次提交/开始编辑都触发一次全量重编译。
- `ProductInformation` 新增 `ApplicationCommit`（读取入口程序集的提交哈希）与 `ApplicationVersionText`（`1.1.0 (4e264cc)` 形式），并由 `ReadCommit(Assembly)` 承载可测试的读取逻辑。第二个壳只要在本仓库内构建就会自动获得该标记。

### 变更

- **壳无关代码抽取为独立程序集 `IcedAstroGrep.AppServices`**：设置持久化、内置插件、结果导出与通知接缝从 WinForms 程序集移出，共 28 个文件 / 8,849 行；`IcedAstroGrep.App` 只留 WinForms 壳（`Windows/**`，60 个文件 / 23,821 行）。新程序集与引擎一样**不设** `UseWindowsForms` / `UseWPF`，其 `deps.json` 的运行时目标是 `.NETCoreApp,Version=v10.0`；新增 `ShellBoundaryTests` 直接断言它不引用 `System.Windows.Forms` / `PresentationFramework` / `PresentationCore` / `WindowsBase` / `System.Drawing.Common`，并断言 WinForms 壳确实引用 WinForms（否则该断言是空转的）。文件移动全部用 `git mv`，命名空间保持 `IcedAstroGrep` / `IcedAstroGrep.Plugins.*` / `IcedAstroGrep.Output` 不变，因此没有调用点因为搬家而改名。
- **`IUserNotifier`：引擎向壳发消息的接缝**：`TextEditors.Open` 原先自己 `MessageBox.Show` 并读取壳的语言资源，现在由壳传入 `IUserNotifier` —— 引擎只传语言键与格式化参数，文案与对话框归壳；传 `null` 则只记日志。WinForms 壳的实现是 `WinFormsNotifier.Instance`，弹出的标题、图标与文案与改造前逐字一致。
- **资源随其使用者一起搬走**：`pdftotext.exe` 与 PDF 插件同处 `IcedAstroGrep.AppServices`，并改为普通 `EmbeddedResource`（不再借用壳的 `Resources.resx` 与 `ResXFileRef`，相应清理了 `Resources.resx` / `Resources.Designer.cs` 中的条目）；`Output.html` / `Output.css` / `Output-fileNameOnly.html` 随导出器移动 —— `HTMLHelper.GetContents` 按"当前程序集名 + `.Output.` + 文件名"拼资源名，因此自动跟随。两者都有测试守护（模板能读出、`pdftotext` 资源名与代码常量一致）。
- **壳专属代码明确留在壳内**：`Theme/`（WinForms 渲染）、`UiConvertors`（`Font` / `SolidColorBrush` / `ComboBox` / 下拉宽度计算）、`ControlInvokeExtensions.InvokeIfRequired`、`Shortcuts`（`API.ShellLink` + `Application.ExecutablePath`）与 `Language`（会遍历 `MainMenu`/`MenuItem`）。唯一触及可见行为的改动是导出 HTML 的颜色转换：`System.Drawing.ColorTranslator.ToHtml`（System.Drawing.Common）改为在壳无关程序集内格式化为 `#RRGGBB`；因 `ConvertStringToColor` 产出的颜色一律来自 `Color.FromArgb`（永不命中已知颜色/系统颜色分支），`ToHtml` 本来就只走 `#RRGGBB` 分支，输出逐字节不变 —— `ShellBoundaryTests` 用 7 组取值锁定该等价性。

### 修复

1.1.0 §已知问题中列出的评审 P2 项，以及两个 Core 遗留问题的处理如下。

- **P2 `Grep` 的上下文行缓冲区随 `ContextLines` 无上限放大**：该缓冲区按 `ContextLines + 1` **逐文件**分配，API 调用方给一个过大的值就等于第一份文件开始 OOM。新增 `Grep.MaxContextLines`（1000）并在 `Execute()` 开头校验，超出范围（含负数，负数原先会在分配时抛异常）即抛 `ArgumentOutOfRangeException`，由搜索线程的错误处理转成可见的搜索错误——与既有的正则探针校验同一思路。WinForms 壳本来就只提供 0–25（`Constants.MAX_CONTEXT_LINES`），因此对现有用户没有任何行为变化。
- **P2 递归遍历缺少符号链接 / 联接点环检测**：一个指向自身祖先的 junction 会让递归一直走下去，直到**不可捕获的** `StackOverflowException` 终止进程。现在每次搜索维护"已走过的真实目录（解析过链接）+ 文件过滤器"的集合，每个真实目录每个过滤器只走一次；顺带消除了"嵌套的起始目录导致同一文件被报告两次"的重复结果。
- **P2 `FilterItem.IsBinaryFile` 只扫描 1 KB（注释称 10 KB）**：改为按注释的意图扫描 10 KB，并且用循环读满缓冲区——`Stream.Read` 允许只返回部分字节，原先单次调用可能只拿到更少内容。仅影响启用了"二进制"排除项的用户，且方向是少漏判。
- **P2 `FilterItem.CheckLongAgainstOption` 对非法值抛 `FormatException`**：大小/长度的排除值不是数字时（手改设置文件、输入未完成），原先每个文件抛一次异常并中断该文件的排除判定；改为 `TryParse`，不匹配且记一条日志（每个排除项只记一次，避免每个文件刷一行）。
- **P2 `PluginManager` 对损坏的插件配置做裸 `Parse`**：`bool.Parse`/`int.Parse` 遇到损坏或半截的 `IcedAstroGrep.plugins.config` 会抛异常并阻断启动。改为 `TryParse` + 警告日志，保留该插件原有状态。
- **P2 `MediaTagsPlugin` 不释放 `TagLib.File`**：`TagLib.File` 持有文件流，改为 `using` 逐文件释放，不再把句柄留到终结器。
- **P2 `MicrosoftWordPlugin` 异常路径遗留隐藏的 WINWORD 进程**：`Load()` 半途失败时已经创建了 Word 实例却只把 `IsUsable` 置为 false，进程无人回收；现在失败路径显式 `Unload()`（退出并释放 COM 对象）。
- **P2 `TextEditors.LaunchEditor` 把值直接拼进命令行**：编辑器参数模板里的 `%1`（路径）与 `%4`（搜索词）现在会转义其中的引号，避免引号提前结束参数、把剩余内容当成额外开关。模板本身是用户为自己的编辑器写的原始命令行，因此这不是信任边界；反斜杠刻意不动（只有紧跟在引号前的反斜杠才特殊，成倍转义会破坏"搜索词里粘贴路径"这一常见用法）。
- **§6.4 两个 Core 遗留点**：`ProductInformation.ApplicationColor`（一个 UI 值）移到它的唯一使用者——WinForms 主题 `LightTheme`，引擎因此**完全不再引用 `System.Drawing`**；`IsPortable` 由硬编码属性改为带说明的 `const`，明确"两个壳都免安装便携、不做打包版"是编译期事实而不是运行期开关。

### 计划中

- 新增 WinUI 3 宿主壳，与现有 WinForms 壳共用同一个引擎与 `IcedAstroGrep.AppServices`。引擎与壳的边界、当前耦合点以及动手前应先做的决定见 [`docs/CORE-SHELL-CONTRACT.md`](docs/CORE-SHELL-CONTRACT.md)。
- 抽取已完成（见上），第二个壳现在只需引用 `IcedAstroGrep.Core` 与 `IcedAstroGrep.AppServices`，不要再引用 `IcedAstroGrep.WinForms`（1.2.0 当时叫 `IcedAstroGrep.App`，见 [未发布] 的改名条目）。
- **数据目录已决定**：两个壳都走免安装便携（**不做 MSIX 打包**），`ApplicationPaths.DataFolder`（入口程序集所在目录）保持不变，`Program.Main` 的"目录不可写"探测继续作为兜底。若将来真要做打包版，改动点就是一处 setter——`EncodingCache` 也写在该目录下，所以这从来不只是设置的问题。
- **日志策略已决定**：`LogClient`（NLog）继续由 Core 拥有、按代码配置写入 `<exe>\Log`，两个壳共用一套；壳若将来要自己的配置，替换 `LogManager.Configuration` 即可。

### 已知问题

- **搜索结果没有数量上限**：`Grep.MatchResults` 会一直增长到搜索结束，超大范围搜索可能耗尽内存。**刻意不做静默截断**——那会让用户以为"就只有这么多结果"，与本引擎"绝不静默漏报"的原则冲突。正确的形态是设置项（上限 + 到达上限时的明确提示），属于新功能，留待 WinUI 3 壳一并设计。
- 会话中途才出现的设置写入失败（例如进程运行期间目录被改成只读）目前只进日志窗口，没有即时界面告警（见 `SettingsIO` 的说明）。
- vendored 目录（`EncodingDetection/Ude`、`IFilterTextReader`）未做逐成员审计：`PreferedEncodingsForStream` 只写不读，但它牵动静态构造函数中的编码枚举逻辑，1.1.0 起就刻意不动。


## [1.1.0] - 2026-09-11

代码评审报告 §6 的 10 项修复全部完成或已标注残留范围；**WinForms 壳在此版本冻结**，后续特性开发转向新的 WinUI 3 壳。引擎已确认不引用任何 UI 框架（`IcedAstroGrep.Core` 的 `deps.json` 只含自身与 NLog）。

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
- **P1-3 剩余（当时的记录；同一轮内已修复，见本版下方"P1-3 剩余项"）**：`FilterLoader` 的 `IStream` 从不释放、单次 `Read` 不保证填满、`CreateStreamOnHGlobal` 的 HRESULT 未检查；`Job.cs` 的句柄与内存泄漏；`VT_BLOB`/`VT_BSTR` 对畸形文档的可致 AV 路径。
- **P1-4 iFilter 失败导致静默漏报**：`FileHandlersPlugin` 在任何 iFilter 失败（读取超时、内容截断、文件过大、格式错误、口令保护等）时，现在既上报搜索错误、又置 `IsFileSkipped` 让默认文本搜索继续处理该文件。原先只上报错误却不回退，文件既没被 iFilter 搜到、也不会走默认搜索，等于漏报。读取超时改为显式设置（60 秒），不再依赖库默认值。`Extensions` 不再返回插件名 `"File Handlers"`——它在插件列表里被当作"扩展名"列显示，改为说明该插件依赖系统 iFilter。
- **P1-4 `FilterSearcher.FileContainsText` 大小写错误**：原先只把行文本 `ToUpperInvariant()` 而搜索词没有同步大写，导致任何非大写搜索词都永远匹配不到；改为按 `ignoreCase` 使用 `StringComparison` 比较，并忽略 `null` 搜索词。
- **P1-4（复核更正）**：评审称 "File Handlers" 插件"默认**启用**"有误。`PluginManager.cs:157` 传给 `PluginWrapper` 的参数是 `internalPlugin: true, enabled: false`（构造函数签名见 `PluginWrapper.cs:64`），该插件本就是**默认关闭**的；"先于默认搜索处理每一个文件"只发生在用户手动启用之后。
- **P1-8 插件把整份文档读入内存**：Excel 与 PDF 插件改为逐行产出——Excel 直接按行消费 `ExcelDataReader` 的前向读取器（不再为每个工作表拼一个大字符串、再切分成数组、再拷进列表），PDF 转换结果用 `StreamReader` 逐行读取（不再 `File.ReadAllLines`），且临时输出在枚举结束时删除（含提前 `break` 的情形）。实测同一份 30 万行 × 120 字符的 xlsx：旧实现托管堆峰值 **295 MB**，新实现 **11 MB**，提取结果逐行完全一致。Word 插件因 OpenXML SDK 会把整份 `word/document.xml` 物化为对象树、无法在不改用前向 XML 读取器的前提下流式化，改为对**主文档部件解压后大小**设 32 MB 上限并显式报错（该值直接取自 zip 中央目录，无需解压）。一个 590 KB 的包解压后是 40 MB 的 document.xml，正说明这道限制的必要性。
- **新发现：Excel 插件在 .NET 10 上完全不可用**：`ExcelDataReader` 的配置构造函数会解析回退代码页 1252，而 .NET Core 默认不注册旧代码页，于是每个 .xls/.xlsx 都抛 `NotSupportedException`。新增 `LegacyEncodingSupport.EnsureRegistered()`（注册 `CodePagesEncodingProvider`，在应用启动与 Excel 插件内各调用一次）。该 provider 随 WindowsDesktop 框架提供，无需显式 `PackageReference`（显式引用会触发 NU1510），故未引入包；`LegacyEncodingSupportTests` 直接断言 `Encoding.GetEncoding(1252)` 可用，守住这一前提。这同时修好了编码检测路径中 `Encoding.GetEncoding(codePage)` 对旧代码页的失败。
- **新发现：Word 插件在没有 styles 部件的文档上 `NullReferenceException`**：`StyleDefinitionsPart` 为 null 时直接解引用。已改为空安全（无样式部件是合法文档）。
- **新发现（评审更正）**：评审 §5-1 将多节点 `dotnet build` 失败归因于 SDK 缺文件有误，实为沙箱 ACL 受限令牌禁止命名管道；关闭沙箱后同一条命令 `Build succeeded`。`dotnet test` 同理（vstest 测试宿主需要 `OpenProcess` 父进程权限），关闭沙箱后 26 个测试全部通过。
- **P2-9 设置写入非原子、读取失败即丢弃全部配置**：`SettingsIO.Save` 改为先把完整文档写入同目录的 `.tmp` 并 `Flush(true)` 落盘，再原子替换目标文件；被替换的旧文件保留为 `.bak`。`Load` 在主文件读取失败（崩溃或断电留下的截断文件）时回退到 `.bak` 并记录警告——原先一次解析失败等于全部设置回到默认值。单条属性应用失败也从 `Console.WriteLine` 改为写日志。
- **P2-8 便携目录不可写时设置静默不保存**：新增 `ApplicationPaths.IsDirectoryWritable`；`Program.Main` 在语言加载后探测程序目录，不可写则记录错误并弹出明确提示（含目录路径与系统错误信息），而不是让之后每一次保存都默默失败。新增 `ApplicationFolderNotWritable` 语言键（en-us）。**刻意不做**回退到 `%APPDATA%`：便携工具的承诺就是数据在程序旁边，静默写到别处比明确告知更糟。
- **P1-9 移除注册表行为**：删除 `Legacy.cs`（612 行）与 `Registry.cs`（355 行）及其 5 个调用点。原先应用启动时会读取 `HKCU\Software\VB and VBA Program Settings\IcedAstroGrep` 迁移旧设置，然后**删除整个键树**——一个"绿色便携"工具不应在未告知的情况下读写并删除注册表。本分支是全新项目，不存在需要迁移的用户，因此直接移除而不是改为询问。未配置文本编辑器时的默认值由"从注册表迁移"改为空列表。iFilter 查找读取 HKLM 的部分（`FilterLoader`）保持不变，它是只读且必要的。
- **P2-7 清理残留死代码**：`EncodingTools` 移除 7 个无任何引用的成员（`DetectOutgoingStreamEncoding`、`DetectOutgoingStreamEncodings`、`GetMostEfficientEncodingForStream`、`IsAscii`、`OpenTextFile`、`OpenTextStream`、`ReadTextFile`；其中 `ReadTextFile` 除了无人调用外本身也是坏的——它分配缓冲区却从不读入文件内容）。`AutoItEncodingDetector.GetBomLengthFromEncodingMode` 与 `CharsetProber.SetOption` 同样无引用，一并移除。**未**对 vendored 目录做逐成员审计：`PreferedEncodingsForStream` 现在只被赋值、无人读取，但它牵动静态构造函数里一整段编码枚举逻辑，本次不动。
- **P2-18 `tools/import-upstream.ps1` 参数化**：原先硬编码作者本机的上下游绝对路径，仓库内无法复用。改为必需的 `-SourcePath`（显式无默认值，并在目录不存在时给出明确错误）与可选的 `-DestinationPath`（默认取脚本所在的仓库）。README 新增"Repository tools"一节说明。
- **P2-17 补齐 `pdftotext` 的第三方许可说明**：核实随附二进制为 Xpdf `pdftotext` **4.01.01**（Copyright 1996-2019 Glyph & Cog, LLC），按 GPL-2 使用（与本项目同许可）。新增 `third-party/xpdf/`：许可与合规说明（含来源、版本、SHA-256、对应源码地址），以及从该二进制本身抓取的帮助文本——因为 Glyph & Cog 明确要求再分发独立可执行文件时**必须一并分发 Xpdf 文档**（README、man/帮助文件与 COPYING）。App 项目会把这两份文件复制到输出目录，README 提示发布时不得删除。
- **P1-3 剩余项（`FilterLoader` / `Job` / `NativeMethods`）**：`FilterLoader` 用单次 `Stream.Read` 填充缓冲区，而该 API 允许只读取部分字节，滤镜可能拿到半填充内容——改用 `ReadExactly`；`CreateStreamOnHGlobal` 的 HRESULT 此前未检查，失败时还会泄漏传入的全局内存块——现在检查并释放；交给滤镜的 COM 流此前从不释放（`fDeleteOnRelease: true` 时等于每次调用泄漏整份文档的内存），改为在 `Load`/`Init` 之后释放，且只释放真正的 COM 对象（非 `readIntoMemory` 路径传的是我们自己的托管 `IStreamWrapper`）。`Job` 构造函数此前泄漏 `AllocHGlobal` 的扩展信息块，且 `SetInformationJobObject` 失败时抛出会连 job 句柄一起泄漏、该类也没有终结器——现在用 `try/finally` 释放内存、失败路径走 `Dispose` 关闭句柄并补上终结器；`AddProcess(int)` 不再泄漏 `Process` 句柄。`NativeMethods` 中 `VT_BLOB` 的长度与指针都来自滤镜，`VT_BSTR`/`VT_LPSTR`/`VT_LPWSTR`/`VT_UNKNOWN` 直接解引用滤镜给的指针：现在拒绝不合理长度（上限 1 MB）、不复制空指针，并注明超出这些防线仍属"需要 `Job` 那种进程外沙箱"的范畴。

### 变更

- **`IcedAstroGrep.Core` 不再引用 UI 框架**：移除 `UseWindowsForms`。Core 唯一触碰的绘图类型是 `System.Drawing.Color`（属基础框架 `System.Drawing.Primitives`），因此 `IcedAstroGrep.Core.deps.json` 现在只含自身与 NLog，没有任何 `Microsoft.WindowsDesktop.App` 框架引用——这是"一 Core 多壳"（WinForms 冻结后新增 WinUI 3 壳）的前提。
- 新增 [`docs/CORE-SHELL-CONTRACT.md`](docs/CORE-SHELL-CONTRACT.md)：引擎与壳的接口、十个事件与线程/取消规则、当前混在 WinForms 程序集里的壳无关代码（约 9,200 行）、动手前应先决定的事项，以及新壳的交接清单。

### 测试

- 新增 `tests/IcedAstroGrep.Core.Tests/FilterItemTests.cs`：覆盖排除项序列化的往返（含 `|`、`<`、反斜杠、UNC 路径、空值、大小显示选项）、旧格式配置的兼容解析、排除项日志详情的拆分。
- 新增 `GrepSearchTests`：否定匹配、上下文行（含未请求时不输出）、仅文件名、最低命中数过滤、扩展名 / 文件名 / 目录排除、子目录递归开关。
- 新增 `GrepRegexTimeoutTests`：`BuildSearchRegEx` 带超时、灾难性回溯模式在同步路径抛 `SearchRegexTimeoutException`、异步路径经 `SearchError` 上报并收敛。
- 新增 `GrepAbortTests`：`AbortAndWait` 在无搜索时返回 true，并确实中止运行中的搜索、join 其线程。
- 新增 `EncodingCacheTests`：`RemoveItem` 同时移除字典项、移除后可重新加入、淘汰与字典内容保持一致、4 线程并发不抛异常。
- 新增 `PluginContractTests`：插件失败但置 `IsFileSkipped` 时**既上报错误又回退到默认搜索**；插件声称已处理文件时会压制默认搜索（说明插件为何必须正确置位）。
- 测试关闭 xUnit 并行执行：`Grep` 与 `EncodingCache` 持有进程级状态，正则超时用例还测时钟。测试总数 26 → **46**。
- 新增 App 侧测试项目 `tests/IcedAstroGrep.App.Tests`（此前测试只能覆盖 Core），并加入 `SettingsIoTests`：往返、不残留 `.tmp`、保留 `.bak`、主文件损坏时从备份恢复、文件缺失或版本不符返回 false、自动创建目录。另有 `ApplicationPathsTests`（Core 侧）覆盖可写探测的可写目录、路径是文件、空路径三种情形。
- 新增 `IFilterIntegrationTests`（App 侧）：对机器上**真实注册的 COM iFilter** 做端到端读取，覆盖 `FilterLoader` 的 IPersistStream 装载、流生命周期与单字符 `Read()`；机器上没有对应 iFilter 时自报跳过而不是判失败。测试总数 56 → **57**。
- 新增 `.github/workflows/ci.yml`：在 `windows-latest` 上 restore / build / test（Release）。此树目标为 `net10.0-windows` 并用到 WinForms 与 WPF，因此只能跑 Windows runner；沙箱环境所需的 `-m:1 -nodeReuse:false` 在 CI 中并不需要。

### 已知问题

- 评审报告 §2/§3 中仍有未处理的 P2 项：`Grep` 的 `_context` 数组随 `ContextLines` 无上限放大、无最大结果数限制、递归遍历缺少符号链接/联接点环检测、`FilterItem.IsBinaryFile` 只扫描 1 KB（注释称 10 KB）且 `CheckLongAgainstOption` 对非法值抛 `FormatException`、`PluginManager` 对损坏配置做裸 `Parse` 会阻止启动、`MediaTagsPlugin` 不释放 `TagLib.File`、`MicrosoftWordPlugin` 异常路径遗留隐藏的 WINWORD 进程、`TextEditors.LaunchEditor` 把路径拼进命令行字符串。
- `EncodingTools.PreferedEncodingsForStream` 现在只被赋值、无人读取；它牵动静态构造函数里一整段编码枚举逻辑，本次未动。
- 会话中途才出现的设置写入失败仍只进日志（用户可在日志窗口看到），没有即时的界面告警。
- vendored 目录（`EncodingDetection/Ude`、`IFilterTextReader`）保持上游原样：只裁剪了确认无引用的叶子成员，没有重排或重格式化，以便将来对比/重新导入上游。

### 计划中

- 继续补充测试覆盖：编码检测本身、命令行导出，以及 App 层插件（Excel / Word / PDF）的提取行为（`tests/IcedAstroGrep.App.Tests` 已经可以承载这类测试）。

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
