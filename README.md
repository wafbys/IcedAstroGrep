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

## Green / portable publish

```powershell
dotnet publish src\IcedAstroGrep.App\IcedAstroGrep.App.csproj -c Release -r win-x64 --self-contained true -o publish\win-x64
```

Zip the `publish\win-x64` folder. Settings, logs, and encoding cache live next to `IcedAstroGrep.exe`.

There is no installer and no Explorer context-menu integration.

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
