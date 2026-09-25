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
- Android pairing (QR + one-time pair code), device presence, remote tasks
- Local-first: settings, sessions, task history, audit logs stay on your device
- External Agent CLI integration: Codex / Claude Code / OpenCode / Pi / generic
- Planned: file transfer, remote screen, MCP, plugins, workflow

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
apps/       windows/ android/
core/       agent/ protocol/ security/ storage/ transport/
providers/  native/ codex/ claude/ opencode/ pi/ generic/
tools/      filesystem/ shell/ process/ app/ system/ clipboard/ screen/
plugins/    sdk/
mcp/
relay/      cloudflare/
docs/ tests/ scripts/
```

C# projects live under `src/` grouped by domain; see `OpenAgent.sln`.

## Getting started

### Prerequisites

- Windows 11 (or Windows 10 1809+) x64
- [.NET 10 SDK](https://dotnet.microsoft.com/) — `winget install Microsoft.DotNet.SDK.10`
- Windows App SDK / WinUI 3 (pulled in as a NuGet package, no separate install)
- Visual Studio 2022+ *optional* — the projects are unpackaged and build from the
  command line with `dotnet build`
- Android: Android Studio + JDK 17+ + Android SDK (API 29+), only for `apps/android`

### Build

```powershell
git clone https://github.com/openagent/openagent.git
cd openagent
dotnet restore
dotnet build -c Release
dotnet test
```

Or use the thin wrappers:

```powershell
./scripts/build.ps1
./scripts/test.ps1
```

### Run

```powershell
dotnet run --project src/apps/windows/OpenAgent.Windows
```

## Security

- End-to-end encrypted device channel (X25519 key agreement, authenticated
  encryption). The Relay only forwards ciphertext.
- Pairing requires QR + one-time pair code + confirmation on both devices.
- Tools carry a risk level; high-risk tools always require approval.
- API keys are stored with OS secure storage (DPAPI / Credential Manager on
  Windows, Android Keystore on Android) — never in SQLite or JSON.
- No telemetry by default. No account required.

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

Early development. Phase 0–2 (solution, Windows core, WinUI 3 UI) are the
current focus. Anything not finished is marked `NOT IMPLEMENTED` in code and
tracked in `docs/dev-log.md` — nothing is faked as working.

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) and
[CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).

## License

[Apache License 2.0](LICENSE).
