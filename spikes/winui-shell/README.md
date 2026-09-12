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

Deliberately not here yet: highlighting and click-to-open in the results pane (M2, waiting on the viewer
decision), and the full options / plug-in / text editor screens (M3b).

## How to run it

```powershell
cd spikes\winui-shell
dotnet restore
dotnet build -c Release
.\bin\Release\net10.0-windows10.0.19041.0\win-x64\winui-shell.exe
```

Put a folder in the first box, a search text in the second, press **Search**.

## What to report back

* build errors, if any — the code was written without being built here (see below);
* whether the file list and the results pane fill in, and whether **Cancel** stops a long search;
* whether a bad pattern on a folder with long lines (for example `(a+)+$`) shows `SEARCH STOPPED: …`
  **in a dialog** rather than hanging or returning nothing;
* whether the plug-in line in the status row shows a count (that is `PluginManager` working from a WinUI
  process, including the embedded `pdftotext`);
* **the language picker**: change it and check that the status line's wording changes (English, German,
  Polish, …) — and that the WinForms shell starts in the same language afterwards, which is the shared
  settings file working in both directions.

## Why it is not built here

This environment cannot restore the Windows App SDK: large downloads come back truncated (measured — a
package request answers HTTP 200 and the stream ends after about 2.3 s with 0.07 MB, while small API
requests succeed). Two ways around it: run the commands above on a machine with a working connection, or
restore there with `dotnet restore --packages .\pkgs`, zip that folder and hand it over, in which case
this side can build it offline.

Framework dependent by default, so it needs the Windows App SDK runtime installed once. Add
`<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` to the project to bundle it the way the
portable WinForms shell ships.
