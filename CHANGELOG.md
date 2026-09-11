# 变更记录

本文件记录 IcedAstroGrep 的主要变更。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

上游基线：[AstroGrep](http://astrogrep.sourceforge.net) 4.4.9（GPL-2.0-or-later）。

## [未发布]

### 修复

- **P0-1 正则表达式可导致搜索线程永久挂死**：所有搜索用 `Regex` 现在都带匹配超时（`Grep.SearchRegExTimeout`，2 秒）；超时被转换为 `SearchRegexTimeoutException` 并以可见的搜索错误上报并中止本次搜索，不再让线程无限期卡死。`FilterSearcher`、`HTMLHelper` 中的正则同样加了超时。搜索用的正则表达式改为每次搜索编译一次（原先是每个文件重新构造，配合 `RegexOptions.Compiled` 等于每个文件做一次动态 IL 编译）。搜索输入校验新增探针匹配，能在开始搜索前就拒绝会触发灾难性回溯的模式。单文件内的取消检查由"每行"放宽为"每 1024 行"一次，配合超时使"取消"按钮真正生效。
- **P0-2 `EncodingCache` 数据结构不一致且无同步**：`RemoveItem` 现在同时从字典中移除条目（原先只移除 LRU 链表节点，导致过期条目被写回磁盘、淘汰逻辑走在不匹配的 LRU 上）；缓存内部加锁并使用线程安全单例；淘汰改为基于实际条目数的自洽实现，LRU 的更新与删除改为 O(1) 且不会与字典内容脱节；`Save` 改为在锁内取快照后再写盘。
- **P0-3 允许同时运行两个搜索**：开始新搜索前先中止旧搜索并等待其线程退出（新增 `Grep.AbortAndWait`），避免两次搜索并发争用插件实例与编码缓存文件。等待前先解绑事件，因此不会与 UI 线程互锁。

### 已知问题

- 评审记录中的 P1/P2 项尚未处理：IFilter 互操作资源泄漏与死循环、File Handlers 插件默认接管所有文件、`FilterItem` 序列化缺少转义、`PDFPlugin` 启动期写盘与子进程无超时/回收、插件整文档读入内存等。

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
