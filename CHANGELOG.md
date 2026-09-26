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

### Added

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
