# Core / shell contract

The WinForms shell's feature set is frozen since **1.1.0** (1.2.0 moved the shell-agnostic code out of
the shell and fixed bugs; it added no features). This document is the hand-off for a second shell
(WinUI 3) and records what already works, what a shell must supply, and the decisions that were
settled before the second shell exists. Keep it current when the seam moves.

The model is **one engine, many shells**: `IcedAstroGrep.Core` is the engine and must never know
which UI is driving it.


## 1. The engine is UI free

Verified, not assumed:

* `IcedAstroGrep.Core.csproj` deliberately sets **no** `UseWindowsForms` / `UseWPF`.
* `src/IcedAstroGrep.Core/bin/.../IcedAstroGrep.Core.deps.json` lists only `IcedAstroGrep.Core` and
  `NLog` — no `Microsoft.WindowsDesktop.App` framework reference.
* Core touches no drawing type at all: `ProductInformation.ApplicationColor` moved to its only
  consumer, the WinForms theme (`LightTheme`), so nothing in the engine references `System.Drawing`
  any more.
* Core contains no reference to `IcedAstroGrep.Windows`, `IcedAstroGrep.Plugins` or
  `IcedAstroGrep.Output`.

A WinUI 3 host can therefore reference Core without dragging WinForms or WPF into the process.

The same holds for `IcedAstroGrep.AppServices`, the assembly that the rest of the shell-agnostic
application moved into (see §5):

* `IcedAstroGrep.AppServices.csproj` also sets **no** `UseWindowsForms` / `UseWPF`.
* Its `deps.json` runtime target is `.NETCoreApp,Version=v10.0` — not
  `Microsoft.WindowsDesktop.App`.
* `ShellBoundaryTests` fails if `IcedAstroGrep.AppServices` ever references `System.Windows.Forms`,
  `PresentationFramework`, `PresentationCore`, `WindowsBase` or `System.Drawing.Common`, and checks
  that the WinForms shell still references `System.Windows.Forms` (otherwise the guard would be
  vacuous).


## 2. What a shell gives the engine

| Input | Notes |
|---|---|
| `ISearchSpec` implementation | Required. `Grep`'s constructor throws `ArgumentNullException` when `EncodingDetectionOptions` is null. |
| `List<PluginWrapper>` via `Grep.Plugins` | Optional. Null means "no plug-ins", and then every file goes through the built-in stream search. |
| A thread to run on | `Execute()` runs on the calling thread; `BeginExecute()` starts a background `Thread`. |
| Cancellation policy | See §4. |
| `ContextLines` within `Grep.MaxContextLines` | 0–1000. The engine checks this in `Execute()` and throws `ArgumentOutOfRangeException` otherwise, because the context ring buffer is allocated per file; the WinForms shell offers 0–25. |
| `IUserNotifier` | Optional, for the engine's messages *to the user* (`TextEditors.Open(opener, notifier)`, null means log only). The engine passes a language key and format arguments; the shell owns the text and the dialog. The WinForms shell uses `WinFormsNotifier.Instance`; see §5.1. |

`ISearchSpec` members: `SearchText`, `StartDirectories`, `StartFilePaths`, `SearchInSubfolders`,
`UseRegularExpressions`, `UseCaseSensitivity`, `UseWholeWordMatching`, `UseNegation`, `ContextLines`,
`ReturnOnlyFileNames`, `FileEncodings`, `EncodingDetectionOptions`, `FileFilter`,
`LongLineCharCount`, `BeforeAfterCharCount`, `FilterItems`.


## 3. What the engine gives back

| Member | Purpose |
|---|---|
| `MatchResults` (`IList<MatchResult>`) | Hits, appended as the search runs. `RetrieveMatchResult(index)` for lookup. |
| `TotalFilesSearched` | Progress counter. |
| `SearchSpec` | The spec the run was constructed with. |
| `Execute()` / `BeginExecute()` | Synchronous / background run. |
| `Abort()` / `AbortAndWait(TimeSpan)` | Cancel; the second also joins the search thread. |
| Events | `SearchingFile`, `FileHit`, `LineHit`, `FileFiltered`, `DirectoryFiltered`, `FileEncodingDetected`, `SearchingFileByPlugin`, `SearchComplete`, `SearchCancel`, `SearchError`. |
| `Grep.BuildSearchRegEx(spec)` | The shared regex construction, including the 2-second match timeout. |
| `Grep.RetrieveLineMatches`, `Grep.WholeWordOnly` | Shared matching helpers so shells highlight exactly what the engine matched. |
| `ProductInformation.ApplicationVersionText` | `1.1.0 (4e264cc)` — version plus the commit the build came from. Show it in the shell's caption so a running copy can be identified. |
| `SearchRegexTimeoutException` | Thrown when a pattern exceeds the timeout; the search is aborted. Surface it, never swallow it. |


AppServices also owns the command line: `CommandLineProcessing.Process(args)` returns a
`CommandLineArguments` that a shell applies to its own controls, so both shells accept exactly the same
switches (the reference list is the class comment) and each one presents the help in its own way. The
WinForms shell opens `frmCommandLine`, whose option table is written by hand.

The results pane is composed there too: `Display.ResultDocument.Build(matches, options)` returns the
text of the pane plus, for every line, the source file/line/column it came from, so every shell lays
results out identically and a shell only has to render it. Note that the context lines a shell can show
are the ones the *search* captured (`ISearchSpec.ContextLines`); the display options select from those.


## 4. Threading and cancellation rules a shell must honour

* **Every event fires on the search thread.** A shell must marshal to its UI thread. The WinForms
  shell does this with `ControlInvokeExtensions.InvokeIfRequired`, which uses a synchronous `Invoke`.
* **`Abort()` is cooperative.** The token is checked between files and inside a file every 1024
  lines. Worst-case latency for a single operation is bounded by the sub-timeouts: 2 seconds for one
  regex match (`Grep.SearchRegExTimeout`), 60 seconds for an iFilter read
  (`FilterReaderOptions.DefaultTimeoutMilliseconds`), 60 seconds for one `pdftotext` conversion.
* **`AbortAndWait(timeout)` exists so a new search cannot race the previous one.** Detach the event
  handlers first, then abort and join — that is what the WinForms shell does, and it is why the
  events are not marshalled onto a UI thread that is blocked in `Join`.
* Do not start a second search without stopping the first: they share the process-wide
  `EncodingCache`, the plug-in instances and the on-disk settings.


## 5. Where the shell-agnostic code lives

The split, measured after the extraction (code lines in `.cs` files at this commit):

| Area | Location | Lines | Shell-agnostic? |
|---|---|---|---|
| WinForms UI | `IcedAstroGrep.WinForms/Windows/**` (60 files) | 23,821 | No — WinForms only |
| Application services | `IcedAstroGrep.AppServices/**` (28 files) | 8,849 | **Yes** — a second shell references this |
| Language resources | `IcedAstroGrep.WinForms/Language/*.xml` + `Windows/Language.cs` | — | No — see §5.1 |
| Engine | `IcedAstroGrep.Core/**` (101 files) | 17,422 | Yes, by construction |

`IcedAstroGrep.AppServices` holds `Core/` (settings, `PluginManager`, `TextEditors`, `Constants`,
`Convertors`, `IUserNotifier`, `CommandLineProcessing`, `Language` with its seven `Language/*.xml`),
`Display/` (`ResultDocument`, the composed results pane) and `Plugins/` (the built-in plug-ins, with
`pdftotext.exe` as an embedded resource) and `Output/` (the exporters and their three embedded
templates). **The namespaces did not change** — `IcedAstroGrep`, `IcedAstroGrep.Plugins.*`,
`IcedAstroGrep.Output` — so no call site had to change namespace along with the files.

### 5.0 Naming rule for shells

**Projects are named after the shell, assemblies after the product.** `IcedAstroGrep.WinForms` and
(later) `IcedAstroGrep.WinUI` both set `<AssemblyName>IcedAstroGrep</AssemblyName>` and
`<RootNamespace>IcedAstroGrep</RootNamespace>`, so:

* both shells build an `IcedAstroGrep.exe`, and they ship in separate folders (so the shared assembly
  name is not a collision);
* `Language.cs` keeps finding `<assembly>.Language.<culture>.xml`, and the resource class stays
  `IcedAstroGrep.Properties.Resources`, which is what the ~30 designer call sites expect;
* the internal namespaces inside the WinForms shell (`IcedAstroGrep.Windows.*`) are historical and
  deliberately untouched — renaming them would be churn without behaviour.

Test projects follow the same rule and are named after what they test: `Core.Tests`,
`AppServices.Tests` (no `UseWindowsForms`, so it proves the services need no UI framework),
`WinForms.Tests`.

### 5.1 What deliberately stayed in the shell

Five things sat in the shell-agnostic folders but are WinForms, WPF or Windows-shell code. They moved
to `IcedAstroGrep.WinForms` rather than into AppServices:

| Type | Why it cannot leave the shell |
|---|---|
| `Theme/` (`ThemeProvider`, `ITheme`, `LightTheme`, `Colors`) | `ToolStripRenderer`, `Pen`, `SolidBrush` — WinForms rendering. |
| `UiConvertors` (`CalculateDropDownWidth`, `ConvertStringToFont`, `ConvertFontToString`, `ConvertStringToSolidColorBrush`, `GetComboBoxEntriesAsString`) | `ComboBox`, `Graphics`, `SystemInformation`, `System.Drawing.Font`, `System.Windows.Media.SolidColorBrush`. The value conversions that only need `System.Drawing.Primitives` stayed in AppServices' `Convertors`. |
| `ControlInvokeExtensions.InvokeIfRequired` | Extension on `ISynchronizeInvoke` taking `System.Windows.Forms.MethodInvoker`. Kept in the `IcedAstroGrep` namespace so its 17 call sites did not have to change. |
| `Shortcuts` | Creates a `.lnk` through `API.ShellLink` from `Windows/Win32.cs`, and uses `Application.ExecutablePath`. |
| `Language` (the text) + `Language/*.xml` | **Moved to AppServices**: key lookup, the seven language files and `LanguageItem`. What stays here is `WinFormsLocalization`, which applies the text to `Control`/`Form`/`MenuItem`/`ToolStripItem` and therefore cannot leave. The engine still asks for *messages* through `IUserNotifier` (§2), which carries language keys rather than sentences. |

The only place where the extraction touched behaviour-visible code is
`Convertors.ConvertColorSettingToHtml` in the exporters: `HTMLHelper` used
`System.Drawing.ColorTranslator.ToHtml` (System.Drawing.Common). Every colour `ConvertStringToColor`
can produce is built with `Color.FromArgb`, so it never sets the known-colour bit and `ToHtml` always
took its `#RRGGBB` branch — which is what made it safe to format the text in-house and drop
`System.Drawing.Common` from the shell-agnostic assembly. `ShellBoundaryTests` pins that equivalence
case by case.



## 6. Decide before building the second shell

Ordered by impact on the WinUI 3 shell.

1. ~~**Where the data lives.**~~ — **decided: portable for both shells, no MSIX.** The WinUI 3 shell is
   built unpackaged and portable exactly like the WinForms one, so `ApplicationPaths.DataFolder` (the
   entry assembly's folder) stays as it is and the whole folder can be carried on a stick or moved
   between machines. `Program.Main` already probes that folder for writability and tells the user when
   it is not, which is what remains of this concern. If a packaged build ever appears, the change is a
   single setter on `ApplicationPaths` — Core's `EncodingCache` writes under that path too, so this was
   never only a settings question.
2. ~~**Extract the shell-agnostic code** (§5) into its own assembly~~ — **done**: settings, plug-ins,
   exporters and the notification seam now live in `IcedAstroGrep.AppServices`, the WinForms shell
   keeps the UI, and `ShellBoundaryTests` keeps the seam honest. A second shell references Core and
   AppServices and never `IcedAstroGrep.WinForms`.
3. ~~**Logging.**~~ — **decided: keep one policy.** `LogClient` (NLog) stays Core-owned and configures
   itself in code to write under `<exe>\Log` with archiving. Both shells are portable and share the data
   folder, so sharing the log there is consistent; a shell that ever wants its own configuration only
   has to replace `LogManager.Configuration`.
4. ~~**Two small Core oddities**~~ — **both resolved**: `ApplicationColor` moved to the WinForms theme
   (`LightTheme`), which was its only consumer, so the engine no longer references `System.Drawing` at
   all; `IsPortable` is now a documented `const` instead of a property that is hardcoded `true`, which
   records "portable only, no packaged build" as the compile-time fact it is.
5. **Settings compatibility.** `FilterItem` serialization (`~v2~` escaping) and the settings XML
   format are the compatibility surface between shells. Keep reusing both so a user can switch
   shells without losing exclusions.


## 7. Hand-off checklist for a new shell

The worked example is the WinUI 3 spike in `spikes/winui-shell/` (deliberately outside
`IcedAstroGrep.slnx`, so the main build keeps its 0 warnings without restoring the Windows App SDK).
A ticked box below means the spike **built and ran on a real machine and that behaviour was observed**
(2026-09-12); a few items are written to the contract but their code path has not been exercised yet,
and those say so instead of being ticked.

- [x] Reference `IcedAstroGrep.Core` and `IcedAstroGrep.AppServices` — never the WinForms assembly
      `IcedAstroGrep.WinForms`. (The spike proves a `net10.0-windows10.0.19041.0` project can reference
      these `net10.0-windows` libraries.)
- [x] Implement `ISearchSpec` (non-null `EncodingDetectionOptions`). The spike reuses `SearchSpec` as
      it is, which is the point — the disk format comes along for free.
- [x] Run the search off the UI thread and marshal all ten events. (Observed: the file list and the
      status line fill in while the window stays responsive.)
- [x] Wire cancellation to `Abort()` / `AbortAndWait()`, and never start a search over a running one.
      (Observed: Cancel stops the search. The spike also unbinds the ten handlers before `AbortAndWait`,
      which is why a second search cannot be fed by the first one's stragglers.)
- [ ] Implement `IUserNotifier` (a language key and format arguments in, your wording and dialog out),
      or pass null to `TextEditors.Open` and keep only the log entry. **Written** (`WinUiNotifier`, a
      `ContentDialog`) and wired to the one message worth a dialog — a regex timeout — but that path
      has not been triggered on a real machine yet.
- [ ] Reuse `SettingsIO` so settings are byte-compatible with the WinForms shell. **Written**: the spike
      reads and writes the shared settings file, so the two shells see each other's options; not yet
      checked by switching shells on a real machine.
- [x] Reuse `PluginManager` and the built-in plug-ins. (Observed: the plug-in count, including the
      embedded `pdftotext` and the plug-in settings file, resolves in the WinUI process.)
- [ ] Reuse `FilterItem.ConvertFilterItemsToString` / `ConvertStringToFilterItems` for exclusions.
      Not used by the spike yet — exclusion editing is M3b.
- [ ] Surface `SearchRegexTimeoutException` and plug-in errors instead of swallowing them — a search
      that quietly returns nothing is the worst failure mode this engine has. **Written**: the spike
      prints `SEARCH STOPPED: …` and raises an `InfoBar` rather than counting the failure as a plain
      error; the timeout itself has not been provoked on a real machine.
- [ ] Keep the portability promise honest: either data really lives beside the executable, or the
      shell says where it lives. The spike inherits `ApplicationPaths` (entry-assembly directory), so it
      is portable by construction, but its own data directory has not been inspected yet.
- [x] Show the build identity. The build stamps `AssemblyMetadataAttribute("GitHash", <commit>)` into
      every assembly in this repository (`Directory.Build.targets`), and
      `ProductInformation.ApplicationVersionText` reads it from the entry assembly — so a new shell
      gets the right value for free as long as the build goes through this repository. The WinForms
      shell puts it in the window caption, the About dialog and the start/stop log lines. (Observed in
      the spike: the window caption carries it, and the spike passes no version information of its own.)


## 8. Verification available today

```powershell
dotnet test IcedAstroGrep.slnx
```

110 tests: 59 in `IcedAstroGrep.Core.Tests` (filtering, negation, context lines, file names only,
minimum hit count, exclusions, subfolder recursion, regex timeout, `AbortAndWait`, encoding cache
consistency and concurrency, `FilterItem` round trips, plug-in contract, and the traversal guards:
context line limits, a real junction loop, overlapping start directories, unreadable exclusion
values, binary detection beyond the first kilobyte), 40 in `IcedAstroGrep.AppServices.Tests` (settings
atomicity, back-up recovery, write probe, real iFilter end-to-end, legacy code pages, the command line
in all its implemented forms, the language files and lookup, the composed results pane and the source
location of every line in it, the HTML document as both a string and a file, and the
AppServices side of the shell boundary: no UI framework reference, the exporter templates embedded
where the exporters look for them, the `pdftotext` resource name matching the code and reading back as
an executable) and 11 in `IcedAstroGrep.WinForms.Tests` (window caption version and commit, the shell
still referencing WinForms, the localizer applying text to a real form, and the HTML colour equivalence
above).

`IcedAstroGrep.AppServices.Tests` deliberately does **not** set `UseWindowsForms`: the services have to
work for any shell, so their tests must not need a UI framework either.

CI runs the same commands on `windows-latest` (`.github/workflows/ci.yml`). Note that the iFilter
integration test reports and skips itself when no filter is registered for `.txt`, so it is not
evidence on a runner without one.
