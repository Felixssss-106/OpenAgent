# Compatibility

Recorded from the actual development machine, not from documentation. Machine-specific
absolute paths are deliberately omitted — record the *kind* of path, not the literal one.

## Machine

| Item | Value |
|---|---|
| OS | Windows 11 25H2, build 10.0.26200.9445 |
| Visual Studio | not installed |
| JDK on `PATH` | 25.0.3 LTS — **not usable for the Android build** (see below) |
| JDK used for Android | 21 (Android Studio bundled JBR at `<AndroidStudio>\jbr`) |
| Gradle | 8.14.3 (local distribution; also committed as the project wrapper) |
| Android Studio | installed |
| Node | 22.22.2 / npm 10.9.7 |
| Android SDK | `%LOCALAPPDATA%\Android\Sdk` (platforms 34/35/36/36.1, build-tools 34–37, NDK, emulator) |
| `ANDROID_HOME` | not set — `android/local.properties` supplies `sdk.dir` |

> In `.properties` files on Windows, write `sdk.dir` and `storeFile` with **forward
> slashes**. A single-backslash path is silently de-escaped by `java.util.Properties`,
> which turns `C:\Users\…` into `C:Users…` and surfaces as `IOException: Invalid file path`
> (AGP SDK locator) or "Keystore file … not found" (signing).

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

- **No Visual Studio** → no MSIX tooling. The WinUI app ships unpackaged and
  self-contained: unzip and run `OpenAgent.exe`.
- **`dotnet publish` needs the `.pri` carried by hand.** The generated resource
  index lands in the build output via the Appx/Pri targets but is not part of the
  publish item set; without it the published app dies at XAML init with
  `0xc000027b`. `OpenAgent.Windows.csproj` adds it to `ResolvedFileToPublish`.
  Smoke-test the publish directory, not `bin`.
- **A manifest authoring warning is a hard failure here.** `81010002:
  Unrecognized Element …` in `app.manifest` means the produced exe will not
  activate; treat it as an error, not a lint nit.
- **WinUI + net10.0** — Windows App SDK 2.5.1 does not impose an upper bound on
  the target framework version, and the WinRT projection assemblies are
  consumable from `net10.0`. This is validated by an actual build, not assumed.
  If a future Windows App SDK release breaks it, the fallback is to move the
  whole repository to the newest TFM that the SDK supports — do **not** leave the
  UI project on an older TFM than the libraries it references.
- **Alt+Space** is claimed by the Windows system menu on most machines. The
  Command Center hotkey therefore falls back through a chain registered at
  startup — see `HotkeyManager` usage in
  `src/apps/windows/OpenAgent.Windows/App.xaml.cs` (`RegisterCommandCenterHotkey`,
  exposed as `HotkeyLabel`).
