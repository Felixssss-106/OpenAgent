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

## 2026-09-26 · Phase 3 — Windows tool set

**Change**

- `OpenAgent.Tools`: `file.list` / `file.read` / `file.write` / `file.move` /
  `file.copy` / `file.delete`, `process.terminate`, `app.close` and
  `screen.capture`, next to the existing `system.get_info`, `process.list` and
  `app.launch`.
- Every path argument goes through `ToolPath` → `PathPolicy`; relative paths are
  combined with the call's working directory but **not** normalised first, so a
  `..` request is refused instead of silently resolved.
- `BuiltInTools.Create()` is the single list the composition root and the tests
  register from.
- `screen.capture` captures with GDI (`BitBlt` + `GetDIBits` over `DllImport`)
  and encodes PNG in managed code. No WinRT, no extra package: the project
  targets `net10.0`, not `net10.0-windows`.

**Reason**

Spec §38 lists the Windows tool set and §18 fixes the risk levels
(`file.delete` / `process.terminate` = high, `file.move` = medium,
`screen.capture` = low). Tools stay in the tool layer and return a `ToolResult`
only — approvals and UI belong to the Agent and the shell (§132).

**Test**

`dotnet build OpenAgent.sln -c Release -p:Platform=x64` → 0 errors / 0 warnings;
`dotnet test OpenAgent.sln -c Release` → 141 passing, including path rejection,
argument validation and a real PNG screenshot.

**Known issues**

- `screen.capture` needs a Windows desktop session; under session 0 or a locked
  screen the capture fails with `OA-5008` instead of returning pixels. A
  `net10.0-windows10.0.26100` project would allow Direct3D/WinRT capture later.
- No undo support yet: mutating tools are declared `Reversible = false` because
  OpenAgent does not back up what they overwrite.

---

## 2026-09-26 · Phase 2 — UI wired to the composition root + motion

**Change**

- `App.xaml.cs` builds the DI composition root on launch
  (`BuildCompositionRoot` → `ServiceRegistration.AddOpenAgent`) and registers an
  `AgentHostAdapter` so the UI library reads real tasks and tools through
  `IAgentHost` instead of sample data. The tray grows a right-click menu
  (open window / open Command Center / exit) and `ExitApplication` releases the
  hotkey, tray icon and provider.
- `TasksPage` and `ToolsPage` enumerate the real `AgentTaskService` log and the
  `ToolRegistry` catalogue. `MainWindow` is frameless
  (`ExtendsContentIntoTitleBar` + `SetTitleBar`) and hides on close so the shell
  lives in the tray.
- `CommandCenterWindow` runs the real chain: `CommandPlanner.Plan` →
  `AgentTaskService.CreateAsync` → `ToolExecutor.ExecuteAsync`, with an approval
  card (`ApprovalService.RequestAsync` → `ResolveAsync`) shown when the
  permission engine asks, plus a live countdown and timeout expiry.
- Motion follows `design/motion.md`: a `Motion` static class holds the duration
  ladder (Instant 120 / State 200 / Exit 240 / Layout 320 / Enter 480 /
  LongPress 1200) and `Motion.Enabled` reads `UISettings.AnimationsEnabled` so
  reduced-motion users get terminal values with no storyboard. `CommandCenter`
  implements M-02 enter (scale 0.97 → 1 + opacity, 320ms, ease-out-expo) and exit
  (scale 1 → 0.98 + opacity, 240ms, ease-in, closes after the storyboard),
  M-14 approval expand (380ms, ease-out-quart, no overshoot), M-17 long-press
  confirm for high/critical risk (1200ms with a progress fill, releases reset),
  and M-24 drawn checkmark on completion (`StrokeDashOffset` over 480ms). Only
  `transform` and `opacity` are animated (section 6); no backdrop blur on the
  overlay.

**Reason**

The spec (sections 35, 97, 132, 155, 165) wants UI → agent service → storage,
not sample data; the motion file is the project's single source for timing and
easing. M-02 was violating it (180ms, scale 0.96, no exit), and high-risk
approvals were one tap away.

**Test**

`dotnet build OpenAgent.sln -c Release -p:Platform=x64` → 0 errors / 0 warnings;
`dotnet test OpenAgent.sln -c Release` → 141 passing; the exe stayed alive for
5s under a smoke launch (no XAML load crash from the motion storyboards).

**Known issues**

- The custom bezier curves in `motion.md` are approximated with the
  like-named WinUI easing functions (`ExponentialEase`, `QuarticEase`,
  `CubicEase`) because the projects target `net10.0` and avoid
  `SplineDoubleKeyFrame`/`KeySpline` churn; durations, scales and the
  reduced-motion gate are exact.
- M-05 (shimmer) and M-11 (spotlight card) are not implemented this phase;
  both are running-state-only and were deferred to keep the overlay idle at 0%
  CPU.
- `CommandPlanner` is still a deterministic keyword router, not a model call —
  it stays until Phase 4 wires a provider.

---

## 2026-09-26 · Phase 4 — credential-free provider core

**Change**

- `OpenAgent.Providers` now hosts the provider abstraction and a credential-free
  default: `IAgentProvider` (Id / DisplayName / Capabilities / CreateSession /
  SendPrompt / Stop / Resume / HealthCheck), `AgentCapabilities` flags
  (Streaming / ToolCalling / Approval / SessionResume / ImageInput /
  InteractiveTerminal / JsonOutput / StructuredOutput / Mcp), and supporting
  records (`ProviderToolInfo`, `AgentSession`, `AgentHealth`, `ProviderStep`).
- `NativeAgentProvider` (`openagent.native`): the always-on floor — deterministic
  keyword routing (`Plan`), Capabilities = ToolCalling | Approval |
  SessionResume | StructuredOutput. `Plan` is a pure static function exported for
  tests; `ExtractTarget` iteratively strips punctuation and whitespace so
  `"spotify" 的` → `spotify`.
- `ProviderRegistry`: aggregates providers, Native is always installed (spec
  §22). `AddOpenAgentProviders(this IServiceCollection)` extension lives in the
  Providers project so the App (top-level host) calls it without creating an
  Agent → Providers cycle.
- `CliDiscovery`: scans PATH for Codex / Claude Code / OpenCode / Pi / Gemini
  via `where.exe` + `--version` probe (spec §24–26). Discovery only — never
  installs, never drives; `KnownClis` table and `ParseVersion` are pure and
  exported for tests.
- `App.xaml.cs` wires `services.AddOpenAgentProviders()` in the composition
  root; `OpenAgent.Windows.csproj` references `OpenAgent.Providers`.
- `tests/OpenAgent.Providers.Tests` (net10.0): 35 tests covering `Plan` routing
  (6 cases), no-match (3), empty tools, `ExtractTarget` edge cases, `SendPrompt`
  happy path and error path, `HealthCheck`, `Stop/Resume` no-ops, `KnownClis`
  coverage, `ParseVersion` (5 positive + 5 negative), `Scan` non-throw guard,
  and `ProviderRegistry` (4 cases).

**Reason**

Spec §22–23 require a provider layer between the UI and the tool executor; the
Native provider is the zero-dependency floor that always works before any CLI
adapter is detected. CLI discovery (§24–26) finds and verifies external agents
without hardcoding flags or driving them — that belongs to each adapter, written
when its CLI is actually present.

**Test**

`dotnet build OpenAgent.sln -c Release -p:Platform=x64` → 0 errors / 0 warnings;
`dotnet test OpenAgent.sln -c Release` → 176 passing (141 baseline + 35
Providers).

---

## 2026-09-26 · Phase 6 (minimal seed) — Transport + Devices page

**Change**

- `OpenAgent.Transport` now has real (if minimal) code instead of an empty
  project: `ITransport`, `DeviceRecord`, and `LocalLoopbackTransport` (spec
  60–65). The loopback transport registers the local Windows host as the only
  device (`Id = "local:" + MachineName`, `ConnectionType = "loopback"`,
  `IsOnline = true`); `SendAsync` is a no-op for the local id and throws
  `NotSupportedException("NOT IMPLEMENTED: non-local device transport")` for any
  other target.
- `IAgentHost.DevicesAsync` + `DeviceSummary` record. The UI library depends only
  on Core/Shared and sees device data through `DeviceSummary`, so it never
  references `OpenAgent.Transport` directly (spec §165 dependency direction);
  `NullAgentHost` returns empty.
- `AgentHostAdapter.DevicesAsync` maps `DeviceRecord` → `DeviceSummary`
  (`Tag` = 本机 / 在线 / 离线). `App.BuildCompositionRoot` registers
  `ITransport` and passes it into the adapter.
- `DevicesPage` now loads real devices via `AgentHost.Current.DevicesAsync()`; the
  fake "Pixel 9" sample row is gone.
- `tests/OpenAgent.Transport.Tests` (net10.0): 5 tests — discovery returns one
  local device, name == MachineName, platform == Windows, local send is a no-op,
  non-local send throws NOT IMPLEMENTED.

**Reason**

The Devices page had hardcoded sample data. The spec wants real device discovery;
the loopback transport is the Phase 6 floor so the page is never fake data, and
LAN/mDNS/Android pairing layers in on top later (Phase 6–7).

**Test**

`dotnet build OpenAgent.sln -c Release -p:Platform=x64` → 0 errors / 0 warnings;
`dotnet test OpenAgent.sln -c Release` → 181 passing (176 + 5 Transport).

---

## 2026-09-26 · UI service tests + LongPressCounter (M-17 timing)

**Change**

- `LongPressCounter`: pure M-17 timing arithmetic (`ProgressMs` / `IsComplete`
  / `ProgressRatio`), extracted from `CommandCenterWindow` so it is unit-testable
  without a `DispatcherQueue`. `CommandCenterWindow` now drives the live progress
  fill and the 1200ms confirm threshold through it.
- `tests/OpenAgent.Windows.UI.Tests` (net10.0-windows, XAML-free, **no
  `UseWinUI`**): a WinUI test-host `Microsoft.TestPlatform.CoreUtilities` loader
  conflict appears when `UseWinUI` is set, so the test project stays
  windows-targeted but without the WinUI build targets; the UI library arrives
  transitively.
- 61 tests: `CommandPlanner.Plan` + `ExtractTarget` (incl. `"spotify" 的`),
  `TaskViewMapper` (StatusLabel / StatusBrushKey / DurationText / SummaryLine),
  `ToolViewMapper` (RiskLabel / RiskBrushKey / RiskBackgroundKey / Glyph),
  `LongPressCounter` (0 / 23 / 24 / 30-tick boundary arithmetic).

**Reason**

The motion logic (M-17 long-press, M-02/M-14/M-24) had no test coverage; the UI
service layer (CommandPlanner / *ViewMapper) was untested. Extracting pure
functions and covering them closes that gap without a UI runtime.

**Test**

`dotnet build OpenAgent.sln -c Release -p:Platform=x64` → 0 errors / 0 warnings;
`dotnet test OpenAgent.sln -c Release` → 242 passing (181 + 61 UI).

---

## 2026-09-26 · Phase 4+ — real CLI Provider adapters (discovery drives execution)

**Change**

- `IProcessRunner` abstracts process spawning so the CLI adapter is unit-testable:
  `ProcessRunResult(ExitCode, StdOut, StdErr)` + `RealProcessRunner` (concurrent
  stdout/stderr reads via `Process`, then `WaitForExitAsync`).
- `CliInvocationProfile`: per-CLI invocation data — `Executables[]`,
  `Capabilities`, and `PrefixArgs` (codex=`exec`, claude=`-p`, opencode=`run`,
  pi/gemini=none; every profile carries `Approval`). `BuildArguments(prompt)` emits
  the prefix then the prompt as a final quoted argument (spaces → `"…"`).
- `CliInvocationProfiles.For(cliId)`: the data table mapping `CliDiscovery` ids to
  profiles (spec §27: discover-and-register, never hardcode CLI flags).
- `CliAgentProvider`: an `IAgentProvider` that drives one CLI per invocation.
  `SendPromptAsync` emits `Thought → ToolCall → ToolResult` steps; a non-zero exit
  code with non-empty stderr becomes an `Error` step; any thrown exception becomes
  an `Error` step. The prompt is always the final quoted argument.
- `AddOpenAgentProviders` now, on Windows, walks `CliDiscovery.Scan()` and for each
  found CLI resolves its profile and registers a `CliAgentProvider` as
  `IAgentProvider`; `IProcessRunner` is registered once. So installing Codex /
  Claude Code / OpenCode makes it selectable with **no shell change** — and if
  none are installed, only the Native provider is present.
- `tests/OpenAgent.Providers.Tests/CliAgentProviderTests.cs` (net10.0): 5 tests via
  a `FakeRunner` — 3-step construction, `-p "打开 notepad"` argument format,
  stderr→Error on non-zero exit, exception→Error, and Capabilities sourced from the
  profile.

**Reason**

Phase 4 left CLI discovery as "find-only"; the spec (§24–27) intends discovery to
make external agents real, selectable providers. Wiring `CliDiscovery.Scan()` to
`CliAgentProvider` via data-driven profiles closes that loop without hardcoding any
CLI flags and keeps the adapter process-spawning isolated behind `IProcessRunner`.

**Test**

`dotnet build OpenAgent.sln -c Release -p:Platform=x64` → 0 errors / 0 warnings;
`dotnet test OpenAgent.sln -c Release` → 247 passing (242 + 5 CLI adapter).

**Known issues**

- The adapter captures stdout/stderr as a single `ToolResult` step — there is no
  streaming or multi-turn session with the underlying CLI yet (spec wants a real
  agent loop in Phase 3/Phase 4+). Native provider still owns the deterministic
  keyword router for in-process intents.
- Profiles are hard-coded per known CLI; an unknown-but-discovered executable gets
  no profile and is skipped (safe default).

---

## 2026-09-26 · Phase 6 (LAN increment) — real UDP device discovery

**Change**

- `LanBeaconFrame`: dependency-free wire codec — a single UTF-8 line
  `OPENAGENT-BEACON v1|id|name|platform|version|port|ticks`. Pure `Encode` /
  `Decode`, fully unit-testable; `Decode` returns null on any malformed input so
  the listener safely ignores port noise.
- `LanDiscoveryOptions`: port (default 47819), beacon interval (3s), and the
  local id/name/platform/version; every value overridable for tests.
- `UdpLanTransport : ITransport, IDisposable`: composes `LocalLoopbackTransport`
  (the local host is always present) and adds UDP broadcast beacon + listener on
  the well-known port. Discovered peers surface as `ConnectionType = "lan"`
  devices. Best-effort throughout — if the socket cannot bind (port taken, no
  permission, headless) it **degrades silently to loopback-only**; `SendAsync`
  for a known peer unicasts to its last-seen endpoint, otherwise best-effort
  broadcast, and never throws.
- `AgentHostAdapter.DevicesAsync` now tags `lan` peers as "局域网" (loopback →
  "本机"); `App.BuildCompositionRoot` registers `UdpLanTransport` (with default
  options) composed over `LocalLoopbackTransport`.
- `tests/OpenAgent.Transport.Tests`: `LanBeaconFrameTests` (7) covers round-trip,
  prefix/field-count/number validation, trailing newline, garbage → null.
  `UdpLanTransportTests` (5) covers loopback-always-present, silent degradation
  when the port is taken, local `SendAsync` no-op, **real peer discovery over
  UDP** (a second `UdpClient` beacon is seen as a `lan` device), and **real
  payload delivery** to a known peer endpoint.

**Reason**

The product is a cross-device control center; Phase 6 was only a loopback floor.
A UDP beacon gives two OpenAgent instances on the same LAN real, zero-dependency
discovery — the first step toward actually controlling another machine. Keeping
it best-effort means the shell never breaks on a locked-down or headless host.

**Test**

`dotnet build OpenAgent.sln -c Release -p:Platform=x64` → 0 errors / 0 warnings;
`dotnet test OpenAgent.sln -c Release` → 259 passing (247 + 12 Transport).

**Known issues**

- Beacon/message bus is plaintext and unpaired — any OpenAgent on the LAN sees
  and can message any other. Pairing, trust and encryption are Phase 6–7.
- `SendAsync` broadcast fallback has no target envelope yet, so a broadcast
  payload reaches every peer; per-device routing arrives with pairing.
- mDNS / Cloudflare Relay are not implemented; this is the broadcast seed.

---

## NOT IMPLEMENTED registry

Every entry below is a real `NotSupportedException("NOT IMPLEMENTED: …")` in
code, not a silent stub.

| Area | What | Planned phase |
|---|---|---|
| `OpenAgent.Transport` | mDNS discovery, Cloudflare Relay, LAN pairing/trust/encryption | Phase 6–7 |
| `OpenAgent.Providers` | CLI adapters: streaming / multi-turn session with the underlying CLI | Phase 4+ |
| `OpenAgent.Plugins` | plugin loader, manifest validation, isolation | Phase 12 |
| `OpenAgent.Mcp` | MCP client and server bridge | Phase 11 |
| `OpenAgent.Agent` | Native agent loop backed by a real model | Phase 3 |
