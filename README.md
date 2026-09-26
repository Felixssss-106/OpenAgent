# OpenAgent

> Open-source AI control center for Windows and Android.
> 开源个人 AI 跨设备控制中心。

**One Agent. Every Device.** / 一个 Agent，连接你的设备。

---

## What is it?

OpenAgent runs a real AI Agent on your Windows machine, and turns your Android
phone into the mobile entry point to that Agent. Not a remote-control app, not
another ChatGPT client — a unified Agent layer that sits between *what you want*
and *what your computer can do*.

```
User intent → Agent plans → asks permission → executes tools → verifies → reports
```

Every sensitive action goes through an explicit approval step. You choose the
AI provider, the Agent CLI, the permission level, and the network path.

## Features

- Windows background Agent + tray + Command Center (`Alt+Space`)
- Tool system with risk levels (safe → critical) and a four-tier permission model
- Task center: plan, tool calls, approvals, logs, output, errors
- Android LAN discovery + command chat over a shared wire format
- Local-first: settings, sessions, task history, audit logs stay on your device
- External Agent CLI integration: Codex / Claude Code / OpenCode / Pi / generic
- Planned: pairing/encryption, Cloudflare Relay, file transfer, remote screen, MCP, plugins

## Architecture

```
                    OpenAgent
                        |
              +---------+---------+
              |                   |
          Windows                Android
              |                   |
       System / Files        Camera / Mic
       Apps / Screen         Files / Voice
       Clipboard             Remote Control
              |                   |
              +---------+---------+
                        |
                    Agent Core
                        |
             +----------+----------+
             |          |          |
          Native AI   External CLI  MCP
             |          |          |
        OpenAgent     Codex       Plugins
        Agent         Claude
                     OpenCode / Pi
```

Layering is mandatory and one-directional:

```
UI → Agent Service → Tool Registry → Permission Manager → Tool Executor
```

## Repository layout

```
src/
  apps/windows/   OpenAgent.Windows (exe) + OpenAgent.Windows.UI (WinUI 3 library)
  core/           OpenAgent.Core / Agent / Transport / Storage / Security
  tools/          OpenAgent.Tools — 11 built-in tools
  providers/      OpenAgent.Providers — IAgentProvider, Native + CLI adapters
  plugins/        OpenAgent.Plugins — contracts only
  mcp/            OpenAgent.Mcp — contracts only
  shared/         OpenAgent.Shared
tests/            8 test projects, 266 tests
android/          Kotlin + Jetpack Compose client (own Gradle project)
docs/             protocol, dev-log, compatibility, development
design/           tokens.css and motion.md — the design source of truth
scripts/          gen-tokens.py, gen-tray-icon.py, build.ps1, test.ps1
```

## Getting started

### Prerequisites

- Windows 11 (or Windows 10 1809+) x64
- [.NET 10 SDK](https://dotnet.microsoft.com/) — `winget install Microsoft.DotNet.SDK.10`
- Windows App SDK / WinUI 3 (pulled in as a NuGet package, no separate install)
- Visual Studio 2022+ *not required* — the app is unpackaged and builds from the
  command line with `dotnet`
- Android (only to build `android/`): Android SDK platform 35, Gradle 8.9+ via the
  committed wrapper, and **JDK 17–21** (Gradle 8.x does not run on JDK 25)

### Build

Every `dotnet` invocation needs `-p:Platform=x64`: the projects declare
`<Platforms>x64</Platforms>` only, so a bare `dotnet build` fails to find a platform.

```powershell
git clone https://github.com/Felixssss-106/OpenAgent.git
cd OpenAgent
dotnet build OpenAgent.sln -c Release -p:Platform=x64
dotnet test  OpenAgent.sln -c Release -p:Platform=x64   # 266 tests
```

Or use the thin wrappers:

```powershell
./scripts/build.ps1
./scripts/test.ps1
```

Android:

```bash
cd android
JAVA_HOME="<AndroidStudio>/jbr" ./gradlew assembleDebug assembleRelease
```

### Install

Get the installer from
[Releases](https://github.com/Felixssss-106/OpenAgent/releases):

| File | Use it when |
|---|---|
| `OpenAgent-1.0.0-x64.exe` | You want a normal installer with a licence and progress page. |
| `OpenAgent-1.0.0-x64.msi` | You are deploying silently or via management software. |

Both are per-machine, let you choose the install folder (default
`%ProgramFiles%\OpenAgent`), and create a desktop plus Start Menu shortcut. The
`.exe` is a wrapper around the same `.msi`. No .NET or Windows App SDK
redistributable is required — the runtime ships inside the package.

```powershell
# silent install, e.g. for management tooling
msiexec /i OpenAgent-1.0.0-x64.msi /qn
```

Neither file is Authenticode-signed yet, so SmartScreen will warn on first run and
`msiexec` needs elevation.

### Run from source

```powershell
dotnet run --project src/apps/windows/OpenAgent.Windows -c Release -p:Platform=x64
```

Build the installers yourself with `dotnet tool restore` then
`./scripts/build-installer.ps1` (needs WiX 7 and the Windows SDK build tools).

## Security

**As of v1.0.0 the LAN channel is not yet secure**: beacons and command envelopes
travel in cleartext, with no pairing, trust or encryption. Do not run this on an
untrusted network.

Implemented today:

- Tools carry a risk level; high-risk tools always require approval.
- Path policy rejects `..` traversal before any file tool runs.
- API keys are stored with OS secure storage (DPAPI / Credential Manager on
  Windows, Android Keystore on Android) — never in SQLite or JSON.
- No telemetry. No account required.

Planned, not implemented (see `docs/dev-log.md` → NOT IMPLEMENTED registry):

- End-to-end encrypted device channel (X25519 key agreement, authenticated
  encryption) and LAN pairing / trust.
- QR + one-time pair code with confirmation on both devices.
- Cloudflare Relay that forwards ciphertext only.

Please report vulnerabilities privately, see [SECURITY.md](SECURITY.md).

## Privacy

Local-first. Settings, sessions, task history, provider config, device metadata
and audit logs stay on the device. Cloud sync is off by default and, when
enabled by the user, never syncs API keys, private keys, or raw file content.

## Agent CLI integration

OpenAgent discovers Agent CLIs already installed on your machine (Codex,
Claude Code, OpenCode, Pi, Gemini CLI, …). It only *discovers* — it never
installs, upgrades, logs in, or copies third-party credentials.

## Status

**v1.0.0 — the first published release.** Both clients build and run: the Windows
app starts from the packaged output and the installers pass Windows Installer
validation, and the Android APK builds from the committed Gradle wrapper and runs
on an API 36 emulator.

What is genuinely working is limited to what the code implements; anything not
finished is a real `NotSupportedException("NOT IMPLEMENTED: …")` rather than a stub
that pretends to succeed. The Agent is still deterministic keyword routing, not a
model loop, and the device channel is still cleartext. See
[`docs/dev-log.md`](docs/dev-log.md) for the registry and
[`AGENTS.md`](AGENTS.md) for the constraints a contributor needs to know.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and
[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## License

[Apache License 2.0](LICENSE).
