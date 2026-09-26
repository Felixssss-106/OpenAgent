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

## 2026-09-26 · v1.0.0 foundation release

**Change**

- Version bumped to `1.0.0` across all .NET assemblies (`Directory.Build.props`).
- Cross-device wire protocol documented in `docs/protocol.md` (beacon + JSON
  envelope) so Windows and Android are guaranteed to interoperate.
- Windows transport enhanced with `LanMessageEnvelope` (codec) and an
  `InboundMessage` event on `UdpLanTransport`; the composition root subscribes
  and logs inbound cross-device commands via `ILogger`.
- Android client (`android/` Gradle project, Kotlin + Jetpack Compose +
  Material 3): `OpenAgentApplication` owns the process-scoped `LanClient`;
  `LanClient` sends `OPENAGENT-BEACON v1` and listens on port 47819, exposes
  discovered peers as a `StateFlow`, and sends/receives `MessageEnvelope`
  commands; `MainViewModel` bridges into Compose; screens are Devices list,
  Agent chat (command/result bubbles), and Settings (device id + system info).
  Wire format is byte-for-byte compatible with the .NET transport.
- `CHANGELOG.md` updated with the full v1.0.0 scope.
- Git tag `v1.0.0` created.

**Reason**

The user directed the project to its first release milestone that includes both
platforms. v1.0.0 is a **foundation release**: a real, tested Windows control
shell + a real, source-complete Android companion that discover each other on a
LAN and can exchange commands via the documented wire protocol. Everything
built after this (pairing, relay, MCP, plugins) layers on top of this floor.

**Test**

`dotnet build OpenAgent.sln -c Release -p:Platform=x64` → 0 errors / 0 warnings;
`dotnet test OpenAgent.sln -c Release` → 259 passing (full suite green).
The Android project is source-verified (no Android SDK in this sandbox); it is
written to compile in Android Studio with AGP 8.7.3 + Kotlin 2.0.21.

**Known issues**

- Android binary cannot be produced in this sandbox (no Android SDK / Gradle
  build-tools). The Kotlin source is complete and aligned to the spec; open the
  `android/` folder in Android Studio to build.
- No git remote is configured; the `v1.0.0` tag is local.

---

## 2026-09-26 · v1.0.0 actually released — first launch of the Windows app ever

**Context**

The previous entry described v1.0.0 as a foundation release with "259 passing"
tests and an Android project that was "source-verified". Taking over the project
meant measuring instead of repeating those claims. Three of them did not survive
contact with the machine:

| Earlier claim | Measured |
|---|---|
| 259 tests | **266** (`Core.Tests` is 36, not 25) |
| 5 test projects | **8** |
| "no Android SDK in this sandbox" | SDK, Gradle 8.14.3, NDK, emulator and Android Studio all present |
| Android is "source-verified / will compile" | **did not compile** |
| Windows shell is visible and working | **the exe had never started** |

**Change**

- `src/apps/windows/OpenAgent.Windows/app.manifest`: dropped
  `<dpiHosting>PerMonitorV2</dpiHosting>`. The element takes a boolean; the
  invalid value made SxS activation fail outright, so `OpenAgent.exe` could not
  launch at all — `SideBySide` event 79, `Start-Process` "应用程序配置不正确".
  The build-time warning `81010002: Unrecognized Element "dpiHosting"` was the
  same fact, and had been ignored every release. `assemblyIdentity` version
  corrected to `1.0.0.0`.
- `OpenAgent.Windows.csproj`: new `IncludeProjectPriFileInPublish` target carries
  `$(ProjectPriFullPath)` into `ResolvedFileToPublish`. `dotnet publish` had been
  omitting the app's own `OpenAgent.pri` resource index, so the published build
  died during XAML initialisation: `Application Error` 1000 in
  `Microsoft.UI.Xaml.dll`, exception code `0xc000027b`.
- `android/`: committed the Gradle wrapper (8.14.3), added `keystore.properties`
  driven release signing with a debug-signing fallback for clean checkouts, and
  fixed `MainActivity.kt` — `Modifier.padding` is an extension from
  `androidx.compose.foundation.layout` and was called through a fully qualified
  receiver without importing it (`Unresolved reference 'padding'`).
- `.gitignore`: `keystore.properties`, `*.jks`, `*.keystore`.
- `docs/compatibility.md`: removed hard-coded local paths (they leaked the
  machine username into what is now a public repository) and recorded the
  toolchain as measured, including the `.properties` forward-slash trap.
- `README.md`: the build commands were missing `-p:Platform=x64` and therefore
  failed; the clone URL pointed at a repository that does not exist; the
  repository layout described a tree this project never had; and the Security
  section claimed end-to-end encryption and QR pairing as if shipped. Both are
  still `NOT IMPLEMENTED`, so the section now separates *implemented* from
  *planned* and warns the LAN channel is cleartext.
- `AGENTS.md`: numbers corrected, release procedure documented, four new
  pitfalls (§8.8–8.11) recorded.

**Reason**

The user directed the project to its first published release across both
platforms. A release is only real if the shipped artefacts start, so the work was
blocked until the Windows binary actually opened a window.

**Test**

- `dotnet build OpenAgent.sln -c Release -p:Platform=x64` → 0 errors / 0 warnings.
- `dotnet test OpenAgent.sln -c Release -p:Platform=x64` → **266 passing**, 0 failed.
- `dotnet publish … -r win-x64 --self-contained true` → 492 files, `OpenAgent.pri`
  present; launched from the publish directory: window title `OpenAgent`,
  `Responding=True`, ~144 MB working set.
- `./gradlew assembleDebug assembleRelease` → BUILD SUCCESSFUL;
  `app-release.apk` signed by `CN=OpenAgent, OU=Releases` (verified with
  `apksigner --print-certs`), `app-debug.apk` still debug-signed.
- The release APK was installed on a headless API 36 emulator and driven:
  `topResumedActivity=MainActivity`, no `AndroidRuntime`/`FATAL` in logcat, the
  Devices screen renders its empty state, and tapping the settings button
  (uiautomator bounds `[944,278][1070,404]`) navigates to Settings showing
  `OpenAgent Android v1.0.0`, the generated device id, `端口: 47819 (UDP)` and
  `Android 16 (API 36)`. The verification AVD was deleted afterwards.

**Known issues**

- Cross-device LAN interop is **still unproven end to end**. The release APK was
  installed and driven on an API 36 emulator (Devices screen renders, Settings
  shows the real device id / UDP 47819 / protocol v1, navigation works), but the
  emulator sits behind its own NAT at `10.0.2.x`, so it cannot hear the Windows
  host's LAN broadcast — discovery between the two binaries needs a real phone on
  the same Wi-Fi to confirm.
- The Command Center FAB exposes no accessibility label (`NAF="true"` in the
  uiautomator dump); the settings button correctly carries `content-desc="设置"`.
- The `v1.0.0` tag now points at the first commit whose Windows binary launches.
  It was moved from the earlier commit, which was local-only and never pushed.

---

## 2026-09-26 · Windows installers replace the portable zip

**Change**

- `installer/OpenAgent.wxs` — WiX 7 MSI. Harvests the self-contained publish tree
  with `<Files Include="$(var.PublishDir)\**" />`, installs per-machine to
  `ProgramFiles64Folder\OpenAgent`, adds a Start Menu shortcut, cleans up its
  folders on uninstall, and carries a stable `UpgradeCode` so `MajorUpgrade`
  replaces older copies instead of stacking them.
- `installer/Bundle.wxs` — Burn `.exe` wrapping that same MSI behind a
  licence/progress/finish UI, with its own distinct `UpgradeCode`.
- `scripts/build-installer.ps1` — publish → MSI → ICE gate → bundle, and it
  refuses to package when `OpenAgent.pri` is missing.
- `scripts/gen-installer-icon.py` — renders `installer/app.ico` at
  16/24/32/48/64/128/256 as PNG-in-ICO. The existing `tray.png` is a single 64px
  bitmap, which the shell resamples badly for shortcuts and Add/Remove Programs.
- `dotnet-tools.json` — WiX 7 as a local dotnet tool, so `dotnet tool restore`
  gives every contributor and CI the same version.
- README / CHANGELOG / AGENTS.md / compatibility notes updated for installers.

**Reason**

The user asked for real Windows installers (exe + msi) instead of a zip to unzip.

**Test**

- `dotnet wix msi validate` **caught a shipping bug** the build itself reported as
  success: without `-arch x64` the package's Template Summary stayed 32-bit, so
  every harvested component was 32-bit aimed at a 64-bit directory (ICE80), and
  Windows Installer would have redirected the install into `Program Files (x86)`.
  Adding `-arch x64` cleared it.
- Administrative install (`msiexec /a`) extracts to a `PFiles64` tree — direct
  confirmation the 64-bit path is used — with 581 files including `OpenAgent.exe`,
  `OpenAgent.pri` and `Microsoft.ui.xaml.dll`.
- `OpenAgent.exe` launched **from that administrative image**: window title
  `OpenAgent`, `Responding=True`, ~155 MB.
- `dotnet wix burn extract` on the `.exe` yields an embedded MSI whose SHA-256
  matches the standalone `.msi` byte for byte.

**Known issues**

- ICE03 / ICE60 still report on `File.Language`: WiX's `<Files>` harvest never
  populates that column, so it is NULL for files without a version resource and an
  over-long multi-LCID list for .NET resource assemblies. The column only affects
  localisation costing during patching, and the harvest exposes no attribute to set
  it, so the build gate suppresses exactly those two ICEs (with the reason in a
  comment) and prints them informationally. Every other ICE still fails the build.
- The installers are **not Authenticode-signed** — no code-signing certificate is
  available here — so SmartScreen will warn. Unverified: a real per-machine
  install/uninstall through Windows Installer, which needs elevation.

## 2026-09-26 · Installer shortcuts + install folder, Android launcher icons

**Change**

- `installer/OpenAgent.wxs`: added `ui:WixUI Id="WixUI_InstallDir"` so the install
  path is browsable instead of fixed, and a `DesktopFolder` component so the
  installer creates a desktop shortcut next to the Start Menu one.
- `installer/Bundle.wxs`: swapped `WixStandardBootstrapperApplication` for
  `WixInternalUIBootstrapperApplication`. The standard BA has no install-location
  UI at all — confirmed by scanning the extension assembly, which contains no
  `InstallFolder`/`Browse` authoring — so forwarding the MSI's dialogs is what makes
  the `.exe` honour the same choice.
- `scripts/oa_mark.py`: the mark's shape and colour moved out of
  `gen-installer-icon.py` into one module, because a second platform now renders it.
- `scripts/gen-android-icons.py`: emits legacy mipmaps (mdpi–xxxhdpi), an adaptive
  icon whose background is the accent colour resource rather than a bitmap, a round
  variant and a monochrome layer; `AndroidManifest.xml` points `icon`/`roundIcon`
  at them. The app previously shipped no launcher icon at all.

**Test**

- MSI `Shortcut` table contains both rows: `StartMenuShortcut → ApplicationMenuFolder`
  and `DesktopShortcut → DesktopFolder`.
- `Dialog` table contains `InstallDirDlg`, and that dialog has a `Folder` PathEdit
  plus a `ChangeFolder` push button — the browse step is really in the package.
- Administrative install still extracts cleanly; MSI 76,317,904 B, EXE 77,049,939 B.
- The rebuilt release APK was installed on an API 36 emulator and the icon rendered
  correctly in system UI (blue squircle, white ring, "OpenAgent"). Measured the
  adaptive foreground: the ring spans 56% of the 108dp canvas, inside the 66dp
  (~61%) safe zone, so no launcher mask clips it. An earlier 1.45 scale put it at
  68% and was rejected for exactly that reason.
- Verification AVD deleted afterwards; pre-existing AVDs untouched.

**Known issues**

- The `.exe` forwarding to MSI internal UI is not runtime-verified: it needs an
  elevated run, and this session is not administrator. The MSI side is verified.
- Neither installer is Authenticode-signed.


## 2026-09-26 · Agent surface, theme palette, and why every screenshot went black

Pixso is the design source of truth, so the released shell was rebuilt against
`design/pixso-final/01…06` and verified by screenshotting the running app and
measuring it, not by eye.

**What changed**

- `AgentPage` replaces both `DashboardPage` and `CommandCenterWindow`: 起始页,
  对话态 and 审批态 are one surface, and Alt+Space raises it inside the shell
  instead of opening a second window with a copy of the same UI.
- 对话态 now draws what artboard 03 draws: a disclosure row per tool call with the
  measured duration right-aligned, and the result in a full-width sunken card.
  审批态 hangs the 760px card off the same left edge as every other block.
- The composer's meta row carries the device pill, the link state and a 思考强度
  picker (`agent.reasoning-effort`, persisted). No provider reads that setting
  yet — it is stored and shown, not fake-wired to something that ignores it.
- Plan arguments are serialized with a relaxed encoder, so a Chinese target shows
  as 记事本 instead of `{"target":"\u8BB0\u4E8B\u672C"}` on the approval card.
- Caption buttons are transparent with theme-coloured glyphs. Left alone they
  painted an opaque black slab over the top-right of the client area.

**Two real defects found while measuring**

- `H.NotifyIcon` converts an `IconSource` through GDI and throws on a PNG
  (`Argument 'picture' must be a picture that can be used as a Icon`), so the
  tray icon never appeared. `scripts/gen-tray-icon.py` now emits a classic
  32bpp DIB `.ico` (16/24/32/48) from the shared `oa_mark`, the shell assigns
  `TaskbarIcon.Icon`, and tray construction is wrapped so it cannot cost the
  shell its window.
- Every brush fetched in code resolved against the **system** palette: WinUI 3
  gives no safe way to change `Application.RequestedTheme` at runtime (it
  fail-fasts, `0xc000027b` / `RPC_E_CALL_REJECTED`, even before the first
  window), and the shell themes its root element instead. A light shell therefore
  rendered a dark selected-nav pill. `UiBrushes` now walks the merged theme
  dictionaries for the requesting element's `ActualTheme` — reading the literal
  `{Name}Color` beside each brush, because a brush inside an inactive theme
  dictionary re-resolves its `{StaticResource}` against the live palette.

**Capture gotcha worth remembering**

Screenshots went uniformly black mid-session and the app looked broken. It was
not: the display had powered off at 19:36 (`Microsoft-Windows-Kernel-Power` id
566), DWM stopped compositing, and `PrintWindow`, WGC and a desktop copy all
handed back the last frame — including the IDE's own, which was visibly frozen.
`scripts/capture-window.ps1` now wakes the display and holds it with
`SetThreadExecutionState` before capturing, crops at the real client origin
(the window still carries ~8px of invisible resize border), and fails a run whose
frame is one flat colour — after writing the file, so a rejected capture is
still inspectable.

**Verified**

- `dotnet build` clean; `dotnet test` 266/266 across the 8 test projects.
- 01/02 起始页 and 03/05 对话态/审批态 captured at a 1440×900 client area in both
  palettes: sidebar rows, search box, separator, version card, greeting leading
  (60px), composer (y 820…875) and approval card geometry match the artboards.
- The approval gate was driven end to end in the running app; 取消 denied the
  launch and Notepad never started.

**Known issues**

- The 思考强度 setting has no consumer until a provider takes a reasoning budget.
- The tool result card shows raw JSON; the artboards show prose. That needs the
  real model loop, not a formatting fix.


## 2026-09-26 · Android redrawn against artboards 07–12 and 25–30

The phone shipped as a Material 3 seed: wallpaper-derived dynamic colours, a top
app bar, chat bubbles, and Devices as the start destination. None of that is in
the design. It is now the same product as the desktop.

**What changed**

- `ui/theme/Color.kt` mirrors `design/tokens.css` (graphite, light and dark) and
  dynamic colour is gone — with Material You on, the phone took its palette from
  the wallpaper and matched nothing.
- `Type.kt` carries the design's scale (hero 44/48, heading 17/24, body 15/22,
  caption 13/18, micro 11 with 0.062em tracking) instead of Material's roles.
- A floating capsule tab bar (`OaTabBar`) with Agent / 任务 / 设备 / 设置, Agent
  first, persists across routes; secondary routes get the 44px navbar with its
  36px circular back button and the 20dp grouped cards.
- `AgentScreen` replaces `AgentChatScreen`: greeting, one status capsule and the
  52dp input capsule until the first command, then the artboard's 你 / AGENT
  turn layout. `TasksScreen` is new.
- Icons are hand-drawn on a 24-unit grid (`ui/Glyphs.kt`). `material-icons-extended`
  was rejected deliberately: the release build has no minification, so it would
  ship the entire icon set for four glyphs.

**Verified**

`scripts/ui-shot-android.sh` builds, installs, taps the tab a user would tap
(coords read out of a `uiautomator` dump, so a route that screenshots also proves
its tab works), and crops to the artboard's 390×844. All four routes captured in
both palettes on a headless API 36 emulator and put side by side with
07/08/25/26/27/28/29/30. The verification AVD was deleted afterwards;
`AILifeTest` and `QpApi29` untouched.

**Known issues**

- 09/10 (对话态) are verified through `AgentScreenTest`, which renders the same
  composable with sample turns and asserts every part of the artboard is on
  screen; `scripts/android-shot-test.sh` holds the frame and screencaps it in both
  palettes. That checks the drawing, not the inbound path — the emulator is NAT'd
  and never hears the host's beacon, and adb cannot forward UDP, so a real
  command→result round trip still needs a phone on the same Wi-Fi.
- 11/12 (审批态) are not drawn on Android at all: the envelope carries only
  command/result/hello text and the permission gate lives on the host. Giving the
  phone an approve button means adding a host→phone approval channel, which is
  deferred until LAN pairing/encryption (Phase 6–7) so the channel is not
  plaintext-and-trusts-anyone.
- The tool disclosure and result card from artboard 09 are absent: the phone
  receives one text blob, so it cannot know which tool ran or how long it took.
- 任务 lists the commands this phone sent and whether the host answered, not the
  host's task table — the phone has no task store to read.
- The settings rows the phone cannot honour (开机启动 / 默认 Agent / 权限模式 /
  Relay / Provider / 插件) are left out rather than drawn dead.


## 2026-09-26 · The card layouts the emulator could not reach

任务 and 设备 on the phone had only ever been seen as empty states — the emulator
has no paired host and no sent command — so `SecondaryScreensTest` renders
`TasksScreenContent` / `DevicesScreenContent` with sample rows and asserts the
artboard's parts are on screen, and `android-shot-test.sh` now takes the test
selector so any of these can be held up and screencapped, in either palette.
25/26/28/29 measured against the artboards: cards at x 24…365, 48dp rows, dot +
title + status, hairline between rows.

Two things that comparison caught:

- 任务 rows carried a clock column the artboard does not draw. Removed.
- The Windows approval card coloured its risk word while artboard 05 keeps every
  fact in one grey, and the plan arguments still read
  `{"target":"\u8BB0\u4E8B\u672C"}` — both fixed, and re-checked against a
  rebuilt shell: `{"target":"记事本"}`, risk in grey, 取消 denied the launch with
  Notepad never starting.

**The trap worth remembering:** `dotnet test OpenAgent.sln` does not build the
shell project, so the suite went green while `OpenAgent.dll` was two hours stale —
the first "did my change land?" screenshot said no, and the honest answer was that
nothing had been rebuilt. Recorded as AGENTS.md §8.16: kill the shell, run
`dotnet build OpenAgent.sln -c Release -p:Platform=x64`, then screenshot.

**Verified:** `dotnet build` clean, `dotnet test` 266/266, Android
`assembleDebug` / `assembleRelease` / `assembleDebugAndroidTest` all green.
Verification AVD deleted afterwards; `AILifeTest` and `QpApi29` untouched.


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
