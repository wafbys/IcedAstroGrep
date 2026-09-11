# Core / shell contract

The WinForms shell is frozen at **1.1.0**. This document is the hand-off for a second shell (WinUI 3)
and records what already works, what a shell must supply, and the decisions that should be made
before the second shell exists. It is written against the code as of 1.1.0; keep it current when the
seam moves.

The model is **one engine, many shells**: `IcedAstroGrep.Core` is the engine and must never know
which UI is driving it.


## 1. The engine is UI free

Verified, not assumed:

* `IcedAstroGrep.Core.csproj` deliberately sets **no** `UseWindowsForms` / `UseWPF`.
* `src/IcedAstroGrep.Core/bin/.../IcedAstroGrep.Core.deps.json` lists only `IcedAstroGrep.Core` and
  `NLog` — no `Microsoft.WindowsDesktop.App` framework reference.
* The only drawing type Core touches is `System.Drawing.Color`
  (`ProductInformation.ApplicationColor`), which ships in the base framework
  (`System.Drawing.Primitives`) and needs no framework reference.
* Core contains no reference to `IcedAstroGrep.Windows`, `IcedAstroGrep.Plugins` or
  `IcedAstroGrep.Output`.

A WinUI 3 host can therefore reference Core without dragging WinForms or WPF into the process.


## 2. What a shell gives the engine

| Input | Notes |
|---|---|
| `ISearchSpec` implementation | Required. `Grep`'s constructor throws `ArgumentNullException` when `EncodingDetectionOptions` is null. |
| `List<PluginWrapper>` via `Grep.Plugins` | Optional. Null means "no plug-ins", and then every file goes through the built-in stream search. |
| A thread to run on | `Execute()` runs on the calling thread; `BeginExecute()` starts a background `Thread`. |
| Cancellation policy | See §4. |

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
| `ProductInformation.ApplicationVersionText` | `1.1.0 (57903c9300c4)` — version plus the commit the build came from. Show it in the shell's caption so a running copy can be identified. |
| `SearchRegexTimeoutException` | Thrown when a pattern exceeds the timeout; the search is aborted. Surface it, never swallow it. |


## 4. Threading and cancellation rules a shell must honour

* **Every event fires on the search thread.** A shell must marshal to its UI thread. The WinForms
  shell does this with `Convertors.InvokeIfRequired`, which uses a synchronous `Invoke`.
* **`Abort()` is cooperative.** The token is checked between files and inside a file every 1024
  lines. Worst-case latency for a single operation is bounded by the sub-timeouts: 2 seconds for one
  regex match (`Grep.SearchRegExTimeout`), 60 seconds for an iFilter read
  (`FilterReaderOptions.DefaultTimeoutMilliseconds`), 60 seconds for one `pdftotext` conversion.
* **`AbortAndWait(timeout)` exists so a new search cannot race the previous one.** Detach the event
  handlers first, then abort and join — that is what the WinForms shell does, and it is why the
  events are not marshalled onto a UI thread that is blocked in `Join`.
* Do not start a second search without stopping the first: they share the process-wide
  `EncodingCache`, the plug-in instances and the on-disk settings.


## 5. What the shell owns today

The WinForms project currently bundles the UI **and** the shell-agnostic application services. The
split, measured at 1.1.0:

| Area | Location | Lines | Shell-agnostic? |
|---|---|---|---|
| WinForms UI | `IcedAstroGrep.App/Windows/**` | 23,243 (52 files) | No — WinForms only |
| Settings persistence | `IcedAstroGrep.App/Core/SettingsIO.cs` | 4,177 (21 files) together with the settings models, `PluginManager`, `TextEditors`, `Constants`, `Convertors`, `LogItems` | **Yes** — no UI types |
| Built-in plug-ins | `IcedAstroGrep.App/Plugins/**` | 3,657 (7 files) | **Yes** — depend on Core plus their own libraries |
| Result exporters | `IcedAstroGrep.App/Output/**` | 1,394 (4 files) | **Yes** |
| Language resources | `IcedAstroGrep.App/Language/*.xml` + `Windows/Language.cs` | — | Mostly; `Language.cs` sits under `Windows/` but is not a form |
| Engine | `IcedAstroGrep.Core/**` | 17,365 (101 files) | Yes, by construction |

So roughly **9,200 lines of shell-agnostic code live inside the UI assembly** and a second shell
would otherwise have to reimplement or duplicate it.


## 6. Decide before building the second shell

Ordered by impact on the WinUI 3 shell.

1. **Where the data lives.** `ApplicationPaths.DataFolder` is the entry assembly's folder — correct
   for the portable, unpackaged build. An MSIX-packaged WinUI app **cannot write there**, so it would
   hit the "application folder is not writable" notice on every start and lose its settings. Decide:
   keep unpackaged/portable for both shells, or make the base directory injectable (a single setter
   on `ApplicationPaths`) and let each shell choose. Core's `EncodingCache` writes under that path,
   so this is not only a settings concern.
2. **Extract the shell-agnostic code** (§5) into its own assembly — for example keep
   `IcedAstroGrep.App` as the WinForms shell and add `IcedAstroGrep.AppServices` for settings,
   plug-ins, exporters and language resources. Without this, the plug-ins and settings format exist
   only inside the WinForms binary.
3. **Logging.** `LogClient` (NLog) is Core-owned and writes under `<exe>\Log`. Decide whether the
   WinUI shell keeps that policy or supplies its own NLog configuration.
4. **Two small Core oddities**: `ProductInformation.IsPortable` is hardcoded `true`, and
   `ApplicationColor` is a UI value living in the engine. Decide whether they stay or move into the
   shells.
5. **Settings compatibility.** `FilterItem` serialization (`~v2~` escaping) and the settings XML
   format are the compatibility surface between shells. Keep reusing both so a user can switch
   shells without losing exclusions.


## 7. Hand-off checklist for a new shell

- [ ] Reference `IcedAstroGrep.Core` only — never the WinForms assembly.
- [ ] Implement `ISearchSpec` (non-null `EncodingDetectionOptions`).
- [ ] Run the search off the UI thread and marshal all ten events.
- [ ] Wire cancellation to `Abort()` / `AbortAndWait()`, and never start a search over a running one.
- [ ] Reuse `SettingsIO` (once extracted) so settings are byte-compatible with the WinForms shell.
- [ ] Reuse `PluginManager` and the built-in plug-ins (once extracted).
- [ ] Reuse `FilterItem.ConvertFilterItemsToString` / `ConvertStringToFilterItems` for exclusions.
- [ ] Surface `SearchRegexTimeoutException` and plug-in errors instead of swallowing them — a search
      that quietly returns nothing is the worst failure mode this engine has.
- [ ] Keep the portability promise honest: either data really lives beside the executable, or the
      shell says where it lives.
- [ ] Show the build identity. The build stamps `AssemblyMetadataAttribute("GitHash", <commit>)` into
      every assembly in this repository (`Directory.Build.targets`), and
      `ProductInformation.ApplicationVersionText` reads it from the entry assembly — so a new shell
      gets the right value for free as long as the build goes through this repository. The WinForms
      shell puts it in the window caption, the About dialog and the start/stop log lines.


## 8. Verification available today

```powershell
dotnet test IcedAstroGrep.slnx
```

57 tests: 49 in `IcedAstroGrep.Core.Tests` (filtering, negation, context lines, file names only,
minimum hit count, exclusions, subfolder recursion, regex timeout, `AbortAndWait`, encoding cache
consistency and concurrency, `FilterItem` round trips, plug-in contract) and 8 in
`IcedAstroGrep.App.Tests` (settings atomicity, back-up recovery, write probe, real iFilter
end-to-end).

CI runs the same commands on `windows-latest` (`.github/workflows/ci.yml`). Note that the iFilter
integration test reports and skips itself when no filter is registered for `.txt`, so it is not
evidence on a runner without one.
