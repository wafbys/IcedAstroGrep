# IcedAstroGrep

Portable Windows file-search (grep) tool. Derived from [AstroGrep](http://astrogrep.sourceforge.net) 4.4.9 under the GNU GPL v2 or later.

## Requirements

- Windows 10/11
- .NET 10 SDK to build
- Runtime is bundled in a self-contained publish (users do not need to install .NET)

## Build

```powershell
dotnet build IcedAstroGrep.slnx
dotnet test IcedAstroGrep.slnx
```

### Building inside a restricted (sandboxed) shell

If commands run under a Windows *restricted token* — a sandbox that confines file operations,
for example — two commands need special handling. Neither is a defect in this repository; in an
unrestricted shell both work exactly as written above.

- **Multi-node MSBuild fails without any diagnostic.** `dotnet build IcedAstroGrep.slnx` exits 1
  reporting `0 Error(s)`, because MSBuild's worker nodes talk over named pipes and a restricted
  token cannot open them. Build single-node instead:

  ```powershell
  dotnet build IcedAstroGrep.slnx -m:1 -nodeReuse:false
  ```

- **`dotnet test` aborts.** The vstest test host calls `Process.EnableRaisingEvents` on its parent
  process, which needs `OpenProcess` rights a restricted token does not grant, so the run ends with
  `Win32Exception (5): Access is denied`. Run the tests with the sandbox disabled, from an
  unrestricted shell, or through a plain test runner that skips vstest.

## Green / portable publish

```powershell
dotnet publish src\IcedAstroGrep.App\IcedAstroGrep.App.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
```

Zip the `publish\win-x64` folder. Settings, logs, and encoding cache live next to `IcedAstroGrep.exe`.

There is no installer and no Explorer context-menu integration.

The publish output also contains `ThirdParty\xpdf\` with the licensing material for the bundled
`pdftotext` executable. Do not strip it from a release: the Xpdf licence requires its documentation
to be distributed together with that binary. See `third-party/xpdf/pdftotext-4.01.01-NOTICE.txt`.

## Repository tools

```powershell
pwsh tools/import-upstream.ps1 -SourcePath <path to an AstroGrep checkout>
```

Imports an upstream AstroGrep source tree into this repository and rewrites the namespaces and paths
on the way. It is a maintenance helper, not part of the build.

## Layout

| Project | Role |
|---|---|
| `IcedAstroGrep.Core` | Search engine, filters, encoding, plugin contract |
| `IcedAstroGrep.IFilter` | Windows IFilter wrapper |
| `IcedAstroGrep.App` | WinForms UI (light theme) |
| `IcedAstroGrep.Core.Tests` | Engine tests |

A WinUI 3 host is not in this tree; Core is kept UI-free so one can be added later.

## License

GPL-2.0-or-later. See `LICENSE` and `NOTICE`.
