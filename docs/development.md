# Development Guide

## Build and test

```powershell
./scripts/build.ps1          # dotnet build -c Release
./scripts/test.ps1           # dotnet test -c Release
dotnet run --project src/apps/windows/OpenAgent.Windows
```

The WinUI project is **unpackaged** and must be built for `x64`:

```powershell
dotnet build src/apps/windows/OpenAgent.Windows -p:Platform=x64
```

## How to add a Tool

1. Add a class in `OpenAgent.Tools` implementing `ITool`.
2. Describe it with a `ToolDefinition`: id, name, description, JSON input
   schema, `RiskLevel`, required permissions, `Reversible`.
3. Register it in `ToolRegistry` (via DI enumeration in the host).
4. The Agent reaches it only through `ToolRegistry` → permission check →
   executor. Never call a tool from UI code.

## How to add a Provider

Implement `IAgentProvider` in `OpenAgent.Providers`. Do not hardcode a CLI's
flags — probe `--version` and `--help`, derive a capability table, then pick an
execution strategy. Fall back to generic terminal mode when a capability is
missing.

## How to add a protocol message

1. Add the DTO to `OpenAgent.Shared` (source-generator friendly,
   `System.Text.Json`).
2. Add the message type constant.
3. Keep protocol version N and N-1 compatible; bump `ProtocolVersion` only for
   breaking changes.

## How to add a UI page

Read `design/pixso-final/index.html`, find the matching artboard, and compare
against `manifest.json#keyDesignDecisions` before writing XAML. Then:

- take colours, type, radii and spacing **only** from `design/tokens.css`
  (mirrored into `Themes/Tokens.xaml`)
- cover Windows light **and** dark; Android has no Tool / Provider / Plugin page
- log the change in `docs/dev-log.md`

## How to write tests

xUnit. One test project per module under `tests/`. Minimum coverage per spec
§183: path validation, tool schema, permission policy, task state machine,
message serialization, retry policy.

## Compatibility notes

Record SDK / package versions and platform caveats in `docs/compatibility.md`.
