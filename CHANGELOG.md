# 变更记录

本文件记录 IcedAstroGrep 的主要变更。格式参考 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
版本号遵循 [语义化版本](https://semver.org/lang/zh-CN/)。

上游基线：[AstroGrep](http://astrogrep.sourceforge.net) 4.4.9（GPL-2.0-or-later）。

## [未发布]

### 已知问题

- 尚未修复的已知缺陷参见仓库内的评审记录（如随源码一同提供）。在发布正式版本前建议优先处理正则匹配无超时、编码缓存一致性、并发搜索互斥等问题。

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
