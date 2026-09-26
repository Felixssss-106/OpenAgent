# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Known limitations

- mDNS, Cloudflare Relay, LAN pairing/trust/encryption are not implemented.
- CLI Provider adapters are single-shot capture (no streaming or multi-turn
  session) and skip unknown discovered CLIs.
- Plugins, MCP, remote file transfer, remote screen and the Plugin SDK are
  contracts only with `NOT IMPLEMENTED` entry points.
- The Android client is a seed (discovery + command chat); it does not yet
  implement pairing, file transfer, or remote screen.

## [1.0.0] — 2026-09-26

First published release. Assets on the GitHub Release: Windows `.msi` and `.exe`
installers and a signed Android `app-release.apk`.

### Added

- Windows installers built with WiX 7: `OpenAgent-1.0.0-x64.msi` (per-machine,
  harvested from the self-contained publish output, ICE-validated) and
  `OpenAgent-1.0.0-x64.exe`, a Burn bootstrapper that wraps the same MSI.
  Both create a Start Menu **and** desktop shortcut and let the user pick the
  install folder (`WixUI_InstallDir`; the exe forwards to the MSI's own dialogs via
  `WixInternalUIBootstrapperApplication`).
- Android launcher icons generated from the same mark as the Windows `.ico`
  (`scripts/oa_mark.py` is the single source): legacy mipmaps for mdpi–xxxhdpi,
  adaptive icon with the accent tile as the background colour layer, a round
  variant, and a monochrome layer for themed icons.

### Fixed

- **The Windows executable could not start at all.** `app.manifest` carried a
  `<dpiHosting>` element with a non-boolean value, which made SxS manifest
  activation fail (`SideBySide` 79, build warning `81010002`). Removed; the app
  now opens its window.
- **Published builds crashed at XAML init.** `dotnet publish` dropped the
  generated `OpenAgent.pri` resource index, so `ms-appx:///` lookups failed with
  `0xc000027b` inside `Microsoft.UI.Xaml.dll`. The project now carries
  `$(ProjectPriFullPath)` into the publish payload.
- **The Android module did not compile.** `MainActivity` called the
  `Modifier.padding` extension without importing it
  (`Unresolved reference 'padding'`).
- Release APKs are now signed with a real release key from
  `android/keystore.properties` instead of falling back to the debug key.

### Added

- Gradle wrapper committed under `android/`, so the APK builds from a clean
  checkout with `./gradlew assembleRelease`.
- Release packaging verified end to end: `dotnet publish` + window smoke test on
  Windows, `apksigner verify --print-certs` on Android.

- Repository scaffolding: solution, 12 C# projects + CI, license and docs
  (Phase 0).
- Windows core: event bus, task state machine, SQLite storage, permission
  policy, tool registry, agent orchestration (Phase 1).
- WinUI 3 shell: main window with sidebar, dashboard, command center, tray,
  single instance, light/dark theme (Phase 2).
- Windows tool set: file ops, process/app control, screen capture via GDI
  (Phase 3).
- Credential-free provider core: `IAgentProvider` abstraction, Native provider,
  CLI discovery and real CLI adapters (Codex / Claude Code / OpenCode) wired as
  selectable providers with data-driven invocation profiles (Phase 4+).
- Cross-device wire protocol: `OPENAGENT-BEACON v1` presence beacon plus a JSON
  message envelope for commands and results (docs/protocol.md, Phase 6).
- LAN transport: `UdpLanTransport` discovers peers on the same UDP port,
  degrades silently to loopback-only if the socket cannot bind, and carries
  messages best-effort (Phase 6).
- Devices page: shows real LAN peers tagged "局域网" alongside the local host
  "本机" (Phase 6).
- Android client (Kotlin + Jetpack Compose + Material 3): a seed app that
  discovers the Windows host over LAN, lists peers, and sends/receives command
  envelopes in an agent chat screen (Phase 8 seed).
- Motion system: duration ladder, reduced-motion gate, M-02 enter/exit,
  M-14 approval expand, M-17 long-press confirm, M-24 drawn checkmark
  (design/motion.md).
- UI service tests: 61 tests covering `CommandPlanner`, `TaskViewMapper`,
  `ToolViewMapper`, `LongPressCounter` (M-17 timing).
- Provider tests: 40 tests covering Native agent routing, CLI discovery,
  `ExtractTarget`, and CLI adapter step construction.
- Transport tests: 24 tests covering beacon codec, envelope codec, real UDP
  peer discovery, payload delivery, and inbound message events.
- Version bump to `1.0.0` across all .NET assemblies.
