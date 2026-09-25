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
