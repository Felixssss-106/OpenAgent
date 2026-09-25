# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Added

- Repository scaffolding: solution, 12 projects, CI, license and docs (Phase 0).
- Windows core: event bus, task state machine, SQLite storage, permission
  policy, tool registry, agent orchestration (Phase 1).
- WinUI 3 shell: main window with sidebar, dashboard, command center, tray,
  single instance, light/dark theme (Phase 2).

### Known limitations

- Transport, Providers, Plugins and MCP projects contain contracts only; their
  implementations land in Phase 4, 6, 11 and 12. Unimplemented entry points
  throw `NOT IMPLEMENTED` and are tracked in `docs/dev-log.md`.
- Android app and Cloudflare Relay are not started yet (Phase 7–8).
