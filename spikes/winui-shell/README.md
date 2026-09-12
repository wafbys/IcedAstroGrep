# WinUI 3 shell spike

The WinUI 3 shell in progress, following the plan in [`docs/WINUI3-EVALUATION.md`](../../docs/WINUI3-EVALUATION.md).
It lives outside `IcedAstroGrep.slnx` on purpose: `dotnet build IcedAstroGrep.slnx` and CI must not have to
restore the Windows App SDK, and this folder is scaffolding until the shell earns a place under `src/`.

## What is here

**M0 — verified on 2026-09-12.** The toolchain works (`net10.0-windows10.0.19041.0` + Windows App SDK
**2.4.0**, unpackaged, x64); a WinUI 3 app can reference `IcedAstroGrep.Core` and
`IcedAstroGrep.AppServices` — both are `net10.0-windows`, so this also settled whether a platform
versioned TFM may reference them; and the shell-agnostic half runs in a WinUI process.

**M1 — the search loop.** This version:

* folder, search text and file types, plus subfolders, case sensitive, whole word, regular expressions,
  negation and file-names-only;
* **Search** / **Cancel**, a progress ring, and a live status line (files searched, files with hits, hit
  lines, errors);
* the file list fills as hits arrive, and the results pane is composed by the shared
  `Display.ResultDocument` — the same "path, blank line, hit and context lines, two blank lines between
  files" layout the WinForms pane uses, with none of it re-implemented here;
* all ten engine events are handled and marshalled to the dispatcher. Per-line events are counted
  **without** marshalling: they fire once per hit line and would flood the UI thread;
* the cancellation order from the contract is spelled out in `StartSearchClick` — detach the handlers,
  then `AbortAndWait`, and only then start the new search. Starting one search over a running one would
  race it over the plug-in instances and the on-disk encoding cache;
* a failed search is **shown, not counted**: a pattern that exceeds the match timeout prints
  `SEARCH STOPPED: …` instead of quietly returning nothing.

**M3a — settings, language and the notifier.** This version also:

* loads the language and the search options from the **shared settings files**, so a user can switch
  between the WinForms shell and this one without losing anything: the checkboxes and the context line
  count come from `SearchSettings`, the wording from `GeneralSettings.Language`;
* has a language picker filled from `Language.AvailableLanguages`; changing it calls `Language.Load`,
  stores the choice and saves the settings — after which the engine's status strings
  (`SearchStarted`, `SearchSearching`, `SearchFinished`, `SearchCancelled`, `SearchFileError`, …) come
  out localized, which is what the language downshift bought. The shell's own labels stay its own
  wording, as the contract says they should;
* writes the search options back to `SearchSettings` when a search starts, so the other shell sees them;
* implements `IUserNotifier` (`WinUiNotifier`, a `ContentDialog`) and uses it for the one message that
  deserves a dialog: a search that stopped because the pattern exceeded the match timeout.

**M2 — the results viewer (route A: WebView2).** This version:

* the results pane is a **WebView2** showing the very document an HTML export produces:
  `MatchResultsExport.BuildResultsAsHTML(settings)` — the same function `SaveResultsAsHTML` writes to a
  file, which a test now asserts (`TheBuiltDocumentIsExactlyWhatTheFileExportWrites`). So the pane has the
  export's layout, its search-options summary and its highlighting, and the two cannot drift apart;
* **click a line to open it** in the configured text editor at that line and column. The shell asks for
  `IncludeSourceLocations` (off by default, so exported files are byte for byte what they were), the lines
  then carry `data-file`/`data-line`/`data-column`, and a small injected script posts the clicked line back
  over `WebMessageReceived` → `TextEditors.Open(opener, notifier)`;
* **Print** works, because the page is HTML: `CoreWebView2.ShowPrintUI()`. This is where route A pays for
  itself, since WinUI 3 has no printing of its own;
* the `ResultDocument` model from §3.1 is *not* what this pane renders — the export markup is — so that
  model stays the definition the WinForms pane and routes B/C build on.

If the build reports that `Microsoft.Web.WebView2.Core` is missing, add a `Microsoft.Web.WebView2` package
reference; the Windows App SDK normally brings it in transitively.

Deliberately not here yet: the full options / plug-in / text editor screens (M3b).

## How to run it

```powershell
cd spikes\winui-shell
dotnet restore
dotnet build -c Release
.\bin\Release\net10.0-windows10.0.19041.0\win-x64\winui-shell.exe
```

Put a folder in the first box, a search text in the second, press **Search**.

## Following the Fluent guidelines

The interface is built the way Microsoft's WinUI guidance asks for it. Each line is checkable in
`MainWindow.xaml`:

| Guideline | How it is followed |
|---|---|
| 4px spacing grid, 24px page margin | `Grid Margin="24" RowSpacing="12"`, 8/16 inside the groups |
| Let the framework own geometry | **no `CornerRadius` anywhere**: `ControlCornerRadius` (4px) and `OverlayCornerRadius` (8px) apply, as [Geometry in Windows](https://learn.microsoft.com/en-us/windows/apps/design/signature-experiences/geometry) describes |
| Respect the system theme, including dark mode | no literal colours and no `FontSize` in the markup: visuals come from each control's own theme resources |
| Typographic hierarchy from the type ramp | `Style="{ThemeResource CaptionTextBlockStyle}"` for the status and plug-in lines ([Text block](https://learn.microsoft.com/en-us/windows/apps/design/controls/text-block)) |
| A label belongs to its input | every `TextBox`/`ComboBox` uses its `Header`, which doubles as the accessibility label |
| Report problems without blocking | an `InfoBar` with `Severity="Error"` instead of a coloured text block; only a search that *stopped* also gets a dialog |
| Keyboard interaction | **Enter** searches, **Escape** cancels (through the Cancel button, which is disabled when nothing runs, so Escape cannot misfire), **Ctrl+F** focuses the search box, **Alt+S / Alt+C** access keys |
| Prevent invalid actions | the input panel is disabled while a search runs |
| Put the caret where work starts | the search box takes focus when the window loads |
| Accessible names | `AutomationProperties.Name` on the file list and the results pane |
| Start at a sensible size | the caption shows the build identity and the window opens at 1100×760 |

Deliberately not done yet: the shell's own labels are still English literals. The guideline-compliant
answer is a `.resw` resource file with `x:Uid` markup, which belongs with M3b once the screens and their
strings have settled. Engine messages already come from the shared language files.

## What to report back

* build errors, if any — the code was written without being built here (see below);
* whether the file list and the results pane fill in, and whether **Cancel** stops a long search;
* whether a bad pattern on a folder with long lines (for example `(a+)+$`) shows `SEARCH STOPPED: …`
  **in a dialog** rather than hanging or returning nothing;
* whether the plug-in line in the status row shows a count (that is `PluginManager` working from a WinUI
  process, including the embedded `pdftotext`);
* **the language picker**: change it and check that the status line's wording changes (English, German,
  Polish, …) — and that the WinForms shell starts in the same language afterwards, which is the shared
  settings file working in both directions;
* **M2**: whether the pane renders the results as a formatted page (not plain text), whether **clicking a
  result line opens it in your text editor at the right line**, and whether **Print** shows the print UI;
* **the Fluent pass**: whether **Enter** starts a search, **Escape** stops one, **Ctrl+F** focuses the
  search box, **Alt+S/Alt+C** work, and whether the window follows your **system theme** (switch Windows to
  dark mode: nothing should stay white or black).

## WinUI is not WPF: what the first real build caught

The first build on a machine with NuGet access (2026-09-12) failed on four C# errors. They are worth
knowing because all four are reflexes from WPF or from an older SDK:

| Error | Cause | Fix applied |
|---|---|---|
| `CS0103: The name 'ProductInformation' does not exist` | `ProductInformation` lives in `IcedAstroGrep.Core`, so `using IcedAstroGrep;` alone is not enough (the spike's main file had both usings, the notifier only the outer one) | added `using IcedAstroGrep.Core;` |
| `CS1061: 'StackPanel' does not contain a definition for 'IsEnabled'` | WPF disables a whole container; WinUI's `UIElement` has no `IsEnabled` — only `Control` does | `SearchInputEnabled(bool)` lists the ten input controls one by one, which also makes the state real for the keyboard and screen readers rather than just visually dimmed |
| `CS7036: no argument given for the required parameter 'printDialogKind'` | current WebView2 SDKs made the dialog kind mandatory | `ShowPrintUI(CoreWebView2PrintDialogKind.Browser)` |
| `WMC9999: Object reference not set` + `WMC1509: No LocalAssembly parameter given during MarkupCompilePass2` | **a cascade, not a XAML problem**: markup compile pass 2 needs the project's own assembly, and there was none because the C# compile had already failed | fix the C# errors first; the XAML errors disappear with them |

## Why it is not built here

This environment cannot restore the Windows App SDK: large downloads come back truncated (measured — a
package request answers HTTP 200 and the stream ends after about 2.3 s with 0.07 MB, while small API
requests succeed). Two ways around it: run the commands above on a machine with a working connection, or
restore there with `dotnet restore --packages .\pkgs`, zip that folder and hand it over, in which case
this side can build it offline.

Framework dependent by default, so it needs the Windows App SDK runtime installed once. Add
`<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` to the project to bundle it the way the
portable WinForms shell ships.
