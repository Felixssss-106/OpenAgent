# Development Log

Format per entry: date · change · reason · test · known issues.

---

## 2026-09-25 · Phase 0 — repository scaffolding

**Change**

- Created `OpenAgent.sln` with 12 C# projects under `src/`, grouped by domain
  (`core/`, `tools/`, `providers/`, `plugins/`, `mcp/`, `shared/`,
  `apps/windows/`).
- Added `Directory.Build.props` (nullable, analyzers, warnings-as-errors) and
  `Directory.Packages.props` (central package version management).
- Added `global.json`, `NuGet.config` (nuget.org only, with source mapping),
  `.gitignore`, `LICENSE` (Apache-2.0), `NOTICE`, `README.md`, `SECURITY.md`,
  `CONTRIBUTING.md`, `CODE_OF_CONDUCT.md`, `CHANGELOG.md`.
- Added `scripts/build.ps1` and `scripts/test.ps1`, and
  `.github/workflows/ci.yml` (restore → build → test on `windows-latest`).

**Reason**

Spec §281 (Phase 0) requires a solution that builds before any feature work.
Spec §20/§269 define the project set and the repository layout; §269's top-level
directory names are kept, while the actual csproj files live under `src/` so the
solution stays navigable.

**Test**

`dotnet restore` + `dotnet build -c Release` must pass with 0 errors and
0 warnings.

**Known issues**

- No .NET SDK was installed on the development machine at the start of this
  phase; .NET 10 SDK was installed as part of this change.
- No Visual Studio is installed, so the WinUI project is built **unpackaged**
  (`WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`) and only via
  the `dotnet` CLI. MSIX packaging is deferred to the release phase.

---

## 2026-09-26 · Phase 2 — WinUI 3 shell visible

**Change**

- `OpenAgent.Windows` (exe) + `OpenAgent.Windows.UI` (library): main window with
  a grouped sidebar, and pages for Dashboard, Tasks, Devices, Tools, Provider,
  Plugins and Settings.
- `CommandCenterWindow`: a 720×480 always-on-top overlay opened with Alt+Space,
  scale+fade entrance, closes on Esc or focus loss.
- Global hotkey via `RegisterHotKey` + window subclassing, single-instance
  `Mutex`, tray icon (double-click opens the command center), and a light/dark
  theme persisted in `ApplicationData.LocalSettings`.
- `scripts/gen-tokens.py` converts `design/tokens.css` (OKLCH) into
  `Themes/Tokens.xaml` (sRGB); `scripts/gen-tray-icon.py` renders the tray glyph.

**Reason**

Spec Phase 2 requires a visible Windows UI over the Phase 1 core. Pages follow
the design boards in `design/pixso-final/` and the token source of truth, so the
shell matches the intended visual system instead of WinUI defaults.

**Test**

`dotnet build OpenAgent.sln -c Release -p:Platform=x64` → 0 errors / 0 warnings;
`dotnet test OpenAgent.sln -c Release` → 98 passing; launching `OpenAgent.exe`
keeps the process alive for 5s (no XAML load crash).

**Known issues**

- Alt+Space is the window system menu on most machines; registration falls back
  to Ctrl+Alt+Space. There is no IPC yet, so a second launch simply exits.
- Window chrome is stock WinUI (system title bar); the frameless look from the
  design boards is not applied.
- Page data is in-memory sample data — the pages are not yet wired to
  `ToolRegistry` / `AgentTaskService` through DI.
- The tray icon supports double-click only; no context menu (open / quit) yet.

---

## NOT IMPLEMENTED registry

Every entry below is a real `NotSupportedException("NOT IMPLEMENTED: …")` in
code, not a silent stub.

| Area | What | Planned phase |
|---|---|---|
| `OpenAgent.Transport` | LAN / Relay connection implementations | Phase 6–7 |
| `OpenAgent.Providers` | Codex / Claude Code / OpenCode / Pi adapters | Phase 4 |
| `OpenAgent.Plugins` | plugin loader, manifest validation, isolation | Phase 12 |
| `OpenAgent.Mcp` | MCP client and server bridge | Phase 11 |
| `OpenAgent.Tools` | Windows tool implementations (file, process, app, screen) | Phase 3 |
| `OpenAgent.Agent` | Native agent loop backed by a real model | Phase 3 |
