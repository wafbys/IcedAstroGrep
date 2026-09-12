# 评估：增加 WinUI 3 壳

> 结论先行：**可行，而且引擎那一侧已经准备好了**，但它不是"再包一层 UI"的活。真正的工作量集中在
> **结果查看器**——现在的结果面板是 AvalonEdit（WPF 专有控件）驱动的，WinUI 3 没有等价物。
> 本文给出可复用/需重建的边界、三条查看器路线、工具链与部署事实、分阶段计划与四个待你拍板的决定。
>
> 核实方式见文末附录；凡是我在本机验证不了的，都明确标成"未核实"。

## 0. 结论摘要

| 判断 | 依据 |
|---|---|
| 搜索、设置、插件、导出四块**零改动可用** | `IcedAstroGrep.Core`（101 文件 / 17,422 行）与 `IcedAstroGrep.AppServices`（28 文件 / 8,849 行）都不引用任何 UI 框架，且 `ShellBoundaryTests` 守住这一点 |
| 需要新建的是 WinForms UI 的显示与交互层 | `IcedAstroGrep.WinForms/Windows/**` 60 文件 / 23,821 行，其中 13 个 Designer 文件 4,643 行直接作废 |
| **唯一的高风险项是结果查看器** | AvalonEdit 集成约 1,065 行 + `frmMain` 里约 450 行展示逻辑 + 命中定位，全部绑定 WPF 控件 API；WinUI 3 无等价控件 |
| 工具链本身成熟 | Windows App SDK **2.4.0** 已是稳定版（2026-08-13）；免打包 + 自包含部署是被文档支持的正式路径 |
| 建议的第一步不是写壳，而是**下沉两项代码** | 结果展示模型 + 命令行处理，1–2 天，收益独立于壳的选择（见 §3） |

## 1. 现状：新壳能直接拿走什么（有证据）

### 1.0 依赖关系（已核对，无反向依赖）

```
IcedAstroGrep.IFilter      叶子：无任何引用（连包都不引用）
        ▲
IcedAstroGrep.Core         引擎：→ NLog。不引用 IFilter
        ▲
IcedAstroGrep.AppServices  → Core + IFilter；插件包 DocumentFormat.OpenXml / ExcelDataReader /
        ▲                    ExcelNumberFormat / TagLibSharp
IcedAstroGrep.WinForms     WinForms 壳：→ AppServices + Core；AvalonEdit + CommandLineParser；
                           UseWindowsForms + UseWPF（**叶节点，没有任何项目引用它**）
```

核对方式与结论：

* 三个共享项目里**没有任何** `ProjectReference` 指向 `IcedAstroGrep.WinForms`（项目名 2026-09-12 由
  `IcedAstroGrep.App` 改来，只改项目名，程序集名仍是 `IcedAstroGrep`；新壳按同一规则叫
  `IcedAstroGrep.WinUI`，见契约文档 §5.0）。
* 152 个共享源文件里，只有两处提到壳的名字，都是良性的：`IUserNotifier.cs` 的一句文档注释、
  以及 `PDFPlugin` 里指向**自己**程序集的资源名常量。没有任何一处 `using IcedAstroGrep.Windows`、
  `IcedAstroGrep.Theme` 或 `Windows.Forms` 类型。
* 共享项目里**没有** `InternalsVisibleTo`：新壳不需要在任何人那里登记，也不依赖友元程序集访问内部类型。
* `ShellBoundaryTests` 在 CI 中强制"AppServices 不引用任何 UI 框架程序集"，所以新壳可见的公开 API
  （199 个 public 类型，含 vendored 编码探测器的实现类）里不可能出现 WinForms/WPF 类型。
* 唯一的**运行时**耦合（不是编译期引用）：`ApplicationPaths` 与 `ProductInformation` 用
  `Assembly.GetEntryAssembly()` 取数据目录与版本/哈希。也就是说"启动的那个 exe 就是这个应用"——
  对第二个壳恰好是想要的行为（各自的 exe 目录放各自的配置，格式字节兼容）。

### 1.1 新壳能直接拿走什么

| 部分 | 位置 | 规模 | 新壳如何使用 |
|---|---|---|---|
| 搜索引擎 | `src/IcedAstroGrep.Core/**` | 101 文件 / 17,422 行 | 实现 `ISearchSpec`、订阅十个事件、调 `Execute()`/`AbortAndWait()` |
| 搜索规格与序列化 | `AppServices/Core/SearchInterfaces.cs` | `SearchSpec : ISearchSpec` | **直接复用**，含磁盘格式 |
| 设置与持久化 | `AppServices/Core/GeneralSettings.cs`、`SettingsIO.cs` | 与 WinForms 壳字节兼容 | 直接复用；用户可在两个壳之间切换而不丢配置 |
| 插件体系 | `AppServices/Core/PluginManager.cs` + `Plugins/**` | 6 个内置插件（Word/WordOpenXML/Excel/iFilter/PDF/媒体标签） | 直接复用；插件状态按"名称+版本+启用+序号"持久化，与程序集身份无关 |
| 结果导出 | `AppServices/Output/**` + 三个 HTML/CSS 模板 | 4 文件 / 1,393 行 | 直接复用，HTML 模板同时是查看器的候选渲染源（见 §4-A） |
| 文本编辑器外启 | `AppServices/Core/TextEditors.cs` + `TextEditor.cs` | 已抽出 `IUserNotifier` 接缝 | 新壳实现 `IUserNotifier` 即可 |
| 旧代码页支持 | `AppServices/Core/LegacyEncodingSupport.cs` | — | 启动时调一次 `EnsureRegistered()` |
| 交接清单 | `docs/CORE-SHELL-CONTRACT.md` §7 | — | 已写好，按条勾选即可 |

### 1.2 "只引用两个项目、调 API"覆盖到什么程度

新壳 = 一个新项目 + 引用 `IcedAstroGrep.Core` 与 `IcedAstroGrep.AppServices`。这个模型**是对的**，
但要分清"已经能调"和"还得自己做"：

| 已经能调（真正的价值，约 26k 行） | 还没覆盖（今天仍在 WinForms 壳里，或必须由壳实现） |
|---|---|
| 搜索执行 + 十个事件 + 正则构建/超时 + `AbortAndWait` | **结果展示模型**：哪些行、哪些高亮区间、行号↔源位置（现在写在 `frmMain` 里且以 AvalonEdit API 表达，§3.1） |
| 搜索规格与设置、插件配置的读写格式（字节兼容） | **命令行处理**：`CommandLineProcessing.cs` 713 + `CLOptions.cs` 139（§3.2） |
| 6 个内置插件 + 外部插件加载（`<数据目录>/Plugins`） | **语言文案查找**：7 个 XML + 键→文本（§3.3） |
| 结果导出（HTML/JSON/CSV/TXT）+ 三个模板 | **打印**：WinUI 3 没有 `PrintDocument`（§4 的 WebView2 可提供） |
| 文本编辑器外启 + `IUserNotifier` 接缝 | 壳自己实现：`IUserNotifier`、十个事件编组到 `DispatcherQueue`、开新搜索前先 `AbortAndWait` |
| 日志（NLog → `<exe>\Log`）、旧代码页注册、主题无关的 `Constants` | 全部 UI：窗口/菜单/工具栏/状态栏/文件列表/选项页/自定义控件/对话框（约 23.8k 行，§2） |

## 2. 需要重建的部分（按代价从高到低）

| 项目 | 现有规模 | WinUI 3 对应物 | 代价 |
|---|---|---|---|
| **结果查看器** | AvalonEdit 集成 ~1,065 行（`TextEditorEx`/两个 Highlighter/行号页边/自动换行/缩放）+ `frmMain` 展示逻辑 ~450 行（`ProcessAllMatchesForDisplay`/`ProcessDocumentForAnchors`/`ProcessFileForDisplay`/`ProcessMatchForDisplay`/`GetEditorAtLocation`） | **无等价控件** | 高，方案见 §4 |
| `frmMain` 主体 | 5,105 + 1,504 Designer | XAML 页面 + 代码隐藏 | 高：菜单、工具栏、状态栏计数、文件列表、拖放、搜索接线、12 处 `InvokeIfRequired` 改 `DispatcherQueue` |
| `frmOptions` | 1,088 + 979 Designer | `SettingsPage` + 导航 | 中高 |
| 打印 | `frmPrint` 412 + `PrintRichTextBox` 134 | **WinUI 3 没有 `PrintDocument`** | 高：需替代（§4-A 的 WebView2 可提供打印），或砍掉改导出 |
| 自定义控件 | `SplitButton` 961、`ColorButton` 541、`ComboBoxEx` 403、`PictureButton` 70、`FilterValueType` 245+151 | XAML 模板 + `CommunityToolkit`（`SegmentedControl`/`SettingsControls`/`TokenizingTextBox`） | 中，逐个重建 |
| 对话框 | 8 个（About/Exclusions/AddEdit×3/Plugins/LogDisplay/CommandLine/Unhandled）约 2,900 行 | `ContentDialog` / 独立窗口 | 中 |
| `Language.cs` | 1,323 行 | 键→文本查找可下沉复用；设计器遍历留 WinForms | 中，见 §3.3 |
| `Win32.cs` | 2,153 行 | ShellLink/图标取用等 P/Invoke **与壳无关**可搬；ListView 专用消息作废 | 中（一半可复用） |
| 主题 | `Windows/Theme/**`（WinForms 渲染器/`ToolStripProfessionalRenderer`） | XAML 主题资源字典 | 低中 |

## 3. 建议先做的下沉（收益独立于壳的选择）

1. **抽出"结果展示模型"（最高优先级）**
   现在的语义——显示哪些行、哪些区间要高亮、行号如何映射回源文件的行/列、上下文行如何合并——全部写在
   `frmMain` 里，且**只以 AvalonEdit 的 API 表达**（`Document.Lines`/`CreateAnchor`/`LineTransformers`）。
   把它提成 shell 中立的数据结构（行集合 + 高亮区间 + 源位置映射），收益有三：
   * 新壳直接复用语义，不必重新发明"上下文行怎么显示"；
   * **这些行为目前完全没有测试**（现有 84 个测试没有一个覆盖结果面板显示什么），抽出后可测；
   * §4 的三种查看器方案都会因此变便宜。
2. **把命令行处理下沉到 AppServices**：`CommandLineProcessing.cs`（713）+ `CLOptions.cs`（139）。两个壳的
   命令行行为必须完全一致，不适合由壳各自实现。
3. **把语言查找下沉**：7 个 `Language/*.xml` + "键→文本"的逻辑放 AppServices，`Language.cs` 只留 WinForms
   设计器遍历（`GenerateXml(Form frm, …)` 那些）。新壳用自己的资源方式呈现，但文案数据只有一份。
4. 为 1 与 3 补测试。

## 4. 结果查看器：三条路（这一项决定整个评估）

| 方案 | 做法 | 优点 | 代价与风险 |
|---|---|---|---|
| **A. WebView2 渲染 HTML** | 把结果渲染成 HTML 交给 WebView2；高亮/配色直接复用现有 `Output.html` + `Output.css` 与 `Convertors.ConvertColorSettingToHtml`；每行带 `data-source-line` 之类属性做命中→源位置映射 | 几乎零重写；**顺带解决打印**（`CoreWebView2.PrintAsync`）；"导出 HTML"与"界面预览"共用一套模板；HTML 转义逻辑已经写好并经过评审 | 需要 WebView2 Runtime（Win10/11 通常自带，也可以固定版本随包分发）；超大结果集的内存与滚动不如原生；文本选择/复制行为受浏览器影响 |
| **B. 原生 `RichEditBox`** | `ITextRange` 逐段设色 | 真原生、无额外运行时 | 大文档性能差、逐段着色慢；行号页边、缩放、自动换行、命中定位全要自己搭，等于重写查看器 |
| **C. 自绘虚拟化** | `Canvas`/Win2D 自己画行 | 性能上限最高、完全可控 | 工期最长、维护成本最高，且要自己做文本选择与无障碍 |

**推荐从 A 起步**，同时做 §3.1 的展示模型——因为无论最终选 A、B 还是 C，展示模型都是共同前提；而 A 能让
第一个可用版本最快出现，也在真实使用中暴露"浏览器渲染结果列表"到底够不够用。

## 5. 工具链与部署：已核实 / 未核实

**已核实（本机，可直接作为"只引用两个项目就够"的依据）**

* `IcedAstroGrep.AppServices` 的 `deps.json` 运行时依赖只有：自身、Core、IFilter、NLog、
  DocumentFormat.OpenXml（+ System.IO.Packaging）、ExcelDataReader、ExcelNumberFormat、TagLibSharp
  ——**没有任何 `Microsoft.WindowsDesktop.App`**。
* `System.Text.Encoding.CodePages.dll`（Excel 插件所需旧代码页的提供者）位于**基础框架**
  `C:\Program Files\dotnet\shared\Microsoft.NETCore.App\10.0.12\`，不是 WindowsDesktop。
* 为此新增的 `IcedAstroGrep.AppServices.Tests` **不设 `UseWindowsForms`** 且全部通过（14 个用例，
  含旧代码页与 iFilter 端到端）——这是"服务不依赖 UI 框架"的运行时证据，而不是推断。

**已核实（微软文档，链接见附录）**

* Windows App SDK **2.4.0** 为当前稳定版（2026-08-13 发布）。
* 免打包（不需要 MSIX）：`<WindowsPackageType>None</WindowsPackageType>`；不要求用户预装运行时：
  再加 `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`。
* 免打包应用的文件/文件夹选择器要用 `Microsoft.Windows.Storage.Pickers`（1.8 引入、2.2 扩充），
  **不要**用需要包标识的 `Windows.Storage.Pickers`。
* 1.8 的一条已知问题：StoragePickers 在**非**自包含部署下会崩（本地化 bug）→ 进一步支持选自包含。
* `PublishSingleFile` 只在"免打包 + 自包含"下受支持，且首次运行会**把依赖解压到 `%TEMP%`**。它不影响我们的
  数据目录（仍是 exe 所在目录），但"单文件"并不等于"零解压"——便携承诺靠的是"自包含 + 文件夹"，不是单文件。
* 最低系统：Windows 10 1809（17763）起；部分 API 需要更高版本。注意当前 `app.manifest` 还声明着 Win7/Win8
  GUID——做 WinUI 3 壳意味着**最低支持系统要上抬**（产品决定，见 §8）。

**未核实（本机网络受限，必须在你的机器上做一次）**

* `net10.0-windows10.0.19041.0` + WASDK 2.4 的实际还原与构建。本机 .NET SDK 是 10.0.401 ✓，但
  **nuget.org 与 github.com 都不可达**，且 `Microsoft.Windows.SDK.NET.Ref`（Windows WinRT 投影包）与
  `Microsoft.WindowsAppSDK` 都不在本机 NuGet 缓存里——所以这条只能靠一次联网 spike 来定。

## 6. 建议的落地顺序与量级

| 阶段 | 内容 | 量级（单人，熟悉 C#/XAML） |
|---|---|---|
| **M0 spike** | 最小 WinUI 3 工程（免打包 + 自包含）→ 引用 Core/AppServices → 跑通一次真实搜索 → 把 `MatchResults` 显示到窗口。同时验证工具链、TFM 与引擎接线 | **1–2 天**（其中"能不能构建"当天就有答案） |
| M1 搜索闭环 | 输入区、开始/取消、进度、文件列表、错误与 `SearchRegexTimeoutException` 提示、`DispatcherQueue` 事件编组 | 1–2 周 |
| M2 结果查看器 | 选定方案 + §3.1 展示模型 + 命中定位/打开编辑器/复制/导出预览 | 1–3 周（方案 A 偏 1，B/C 偏 3） |
| M3 设置与插件界面 | 选项页、排除项编辑、文本编辑器配置、插件管理 | 2–3 周 |
| M4 细节对齐 | 主题、快捷键、CLI、日志窗口、关于、iFilter 提示、打印或替代 | 1–2 周 |

合计约 **6–10 周**，不含 WinForms 壳继续修 bug 的时间。M0 的结论出来之前，这个区间的不确定度主要来自
"WASDK 在本机的构建体验"。

## 7. 替代路线（同一个引擎，代价不同）

* **WPF 壳**：AvalonEdit 本身就是 WPF 控件，结果查看器（含高亮器、行号页边、缩放、自动换行）**几乎原样搬**；
  打印有 `PrintDialog`；XAML 语法与 WinUI 3 接近。代价是外观不够"Win11"，长期看 WPF 的演进不如 WinUI 3。
  如果目标是"以最小代价拿到第二个壳、验证多壳架构"，WPF 明显更划算。
* **Avalonia 壳**：跨平台，但这个引擎是 Windows 专有的（iFilter/COM/pdftotext/TagLib 路径、Win32 P/Invoke），
  跨平台收益有限，还多一个第三方 UI 依赖。

无论选哪条，Core 与 AppServices 都**不需要再动**——这正是 1.2.0 把壳无关代码抽出来的价值。

## 8. 需要你拍板的四件事

1. **最低支持的 Windows 版本**：WinUI 3 意味着 ≥ Win10 1809（当前清单声明到 Win7）。
2. **结果查看器方案**（§4 的 A/B/C）——影响 1–2 周工期与"结果面板体验是否与现在一致"。
3. **打印**：保留（走 WebView2 打印）、放弃（改为导出后交给系统默认程序打开），还是用 WinUI 3 的打印 API。
4. **是否先做 §3 的下沉**：我建议先做 1 与 2（1–2 天），它们的收益不依赖最终选哪个壳；做完之后再开 M0。

## 附录：本次评估的核实方式

* 代码规模与耦合统计：按目录/文件统计 `.cs` 行数；对 `frmMain.cs`（5,105 行）统计 AvalonEdit 相关
  引用（`Document`/`CreateAnchor`/`TextArea`/`LineTransformer`/缩放选择 API 共约 170 处引用行）与展示相关
  方法清单。
* 复用可行性：确认 `AppServices/Core/SearchInterfaces.cs` 中的 `SearchSpec : ISearchSpec`；
  `deps.json` 运行时目标为 `.NETCoreApp,Version=v10.0`（无 WindowsDesktop）。
* "默认排除项不含 Binary"：读取代码默认值 `Constants.DefaultFilterItems` 与本机实际
  `IcedAstroGrep.search.config`，两者都只有 `File^Extension` 与 `Directory^Name`。
* WinForms 壳可用性：实机启动 Release 构建，日志显示 `version 1.2.0 (c0283b6) (Portable)` 与
  `Displaying main search window.`，无 ERROR/Exception。
* 工具链事实来源（外部文档）：
  * [Windows App SDK 2.0 release notes](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-2-0?pivots=stable)
  * [Windows App SDK 1.8 release notes](https://learn.microsoft.com/en-us/windows/apps/windows-app-sdk/release-notes/windows-app-sdk-1-8?pivots=stable)
  * [Distribute an unpackaged WinUI 3 app](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/unpackage-winui-app?tabs=csharp)
* 未能核实：`Microsoft.WindowsAppSDK` 的还原与构建（本机 nuget.org/github.com 均不可达，
  相关包不在本地缓存）。
