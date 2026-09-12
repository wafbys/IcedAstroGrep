# M0 spike: a WinUI 3 window on top of the engine and the services

This is the first step of the WinUI 3 plan in [`docs/WINUI3-EVALUATION.md`](../../docs/WINUI3-EVALUATION.md):
the smallest possible WinUI 3 app that still proves the things the second shell depends on.

## What a successful run proves

1. **The toolchain works**: `net10.0-windows10.0.19041.0` + Windows App SDK **2.4.0**, unpackaged
   (`WindowsPackageType=None`, no MSIX), x64.
2. **A WinUI 3 app can reference our libraries**: the project references `IcedAstroGrep.Core` and
   `IcedAstroGrep.AppServices` and nothing else — no `IcedAstroGrep.WinForms`, no WinForms, no WPF.
   Both are `net10.0-windows` assemblies, so this also settles the "can a platform versioned TFM
   reference them" question.
3. **The shell-agnostic half really works from a WinUI process**: the window shows the build identity
   from `ProductInformation` (Core), the built-in plug-in count from `PluginManager` (AppServices, which
   also exercises the embedded `pdftotext` resource and the settings files), and a localized string from
   `Language` (AppServices).

A window whose four lines all show values means all three are true. Anything that throws is caught and
printed on the line it belongs to, so a partially working spike still tells you which layer failed.

## What it deliberately is not

* **Not part of the solution.** `IcedAstroGrep.slnx` does not list it, so `dotnet build
  IcedAstroGrep.slnx`, `dotnet test` and CI never try to restore the Windows App SDK. Delete this
  folder once M1 exists.
* **No search yet.** The search loop, the ten events, the `DispatcherQueue` marshalling and
  cancellation are M1; the results viewer is M2. The display model those need is being extracted into
  AppServices separately, and is viewer independent.
* **Not verified by me.** See "Status" below.

## How to run it

```powershell
cd spikes\winui-m0
dotnet restore
dotnet build -c Release
.\bin\Release\net10.0-windows10.0.19041.0\win-x64\winui-m0.exe
```

Framework dependent by default, so it needs the Windows App SDK runtime installed once. To make it
self contained (no prerequisite, which is how the portable WinForms shell ships), add
`<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>` to the property group in
`winui-m0.csproj`; that pulls the full runtime into the output folder instead.

## If NuGet is unreachable or stalls

Large downloads through this environment are unreliable (measured: a package download that returns
HTTP 200 and then ends after ~0.07 MB, while small API requests succeed), and the Windows App SDK is a
large package. Practical alternatives, cheapest first:

1. **Run the commands above on a machine with a working connection** and report the output back
   (build errors, the four lines in the window, or the exception text).
2. **Restore there, build here.** Run `dotnet restore` for this project on a working machine, then copy
   `%USERPROFILE%\.nuget\packages` (or just the `microsoft.windowsappsdk*`, `microsoft.windows.sdk.*`
   and `microsoft.web.webview2` folders plus their dependencies) into this environment's
   `%USERPROFILE%\.nuget\packages`. NuGet resolves from the local cache without a network, so the build
   then works here and I can iterate on it.
3. **Give me a proxy** if one exists on your side (this environment has none configured: WinHTTP is
   direct, no `HTTP(S)_PROXY` variables, `NuGet.Config` has only the default nuget.org source). Set it
   and I will use it.

## Status

**Built and run successfully on 2026-09-12**, on a machine with a working NuGet connection. So the
project file, the manifest and the XAML below are known good as written, and all three things this
spike exists to prove are confirmed:

* the toolchain works (`net10.0-windows10.0.19041.0` + Windows App SDK 2.4.0, unpackaged, x64);
* a WinUI 3 app can reference `IcedAstroGrep.Core` and `IcedAstroGrep.AppServices` — both are
  `net10.0-windows` assemblies, and referencing a lower platform version works as expected;
* the shell-agnostic half works from a WinUI process.

It has not been built inside the assistant's environment: that environment could not finish the
Windows App SDK restore (large downloads come back truncated there; see "If NuGet is unreachable or
stalls"). Keep that in mind only if you change the project, since a restore there will fail again.
