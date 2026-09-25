# Compatibility

Recorded from the actual development machine, not from documentation.

## Machine

| Item | Value |
|---|---|
| OS | Windows 11 25H2, build 10.0.26200.9445 |
| Visual Studio | not installed |
| JDK | 25.0.3 LTS (`JAVA_HOME=D:\Develop\JDK`) |
| Node | 22.22.2 / npm 10.9.7 |
| Android SDK | `C:\Users\xfmk\AppData\Local\Android\Sdk` (API 34/36/36.1) |
| `ANDROID_HOME` | not set (needed from Phase 8) |

## .NET

| Item | Value |
|---|---|
| SDK | .NET 10 (`Microsoft.DotNet.SDK.10`, 10.0.401) |
| Runtimes present before install | Microsoft.NETCore.App 6.0.36 / 8.0.31, WindowsDesktop.App 6.0.36 |
| Target framework (non-UI) | `net10.0` |
| Target framework (WinUI) | `net10.0-windows10.0.26100.0` |

## Package versions

Pinned centrally in `Directory.Packages.props`.

| Package | Version | Note |
|---|---|---|
| Microsoft.WindowsAppSDK | 2.5.1 | WinUI 3 / Windows App SDK |
| Microsoft.Windows.SDK.BuildTools | 10.0.28000.2705 | |
| Microsoft.Extensions.{DependencyInjection,Hosting,Logging} | 10.0.12 | |
| Microsoft.Data.Sqlite | 10.0.12 | chosen over EF Core: single package, no model conventions, SQL stays explicit |
| H.NotifyIcon.WinUI | 2.4.1 | tray icon |
| WinUIEx | 2.9.3 | HWND helpers |
| xunit | 2.9.3 | |
| Microsoft.NET.Test.Sdk | 18.10.1 | |

## Platform caveats

- **No Visual Studio** → no MSIX tooling. The WinUI app is built unpackaged and
  self-contained. Packaging comes in the release phase.
- **WinUI + net10.0** — Windows App SDK 2.5.1 does not impose an upper bound on
  the target framework version, and the WinRT projection assemblies are
  consumable from `net10.0`. This is validated by an actual build, not assumed.
  If a future Windows App SDK release breaks it, the fallback is to move the
  whole repository to the newest TFM that the SDK supports — do **not** leave the
  UI project on an older TFM than the libraries it references.
- **Alt+Space** is claimed by the Windows system menu; see
  `docs/windows.md` for the hotkey fallback chain.
