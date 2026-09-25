# Contributing to OpenAgent

Thanks for wanting to help. This project optimises for trustworthy software, not
for impressive demos — that shapes the rules below.

## Ground rules

1. **No fake completion.** If something is not implemented, throw
   `NotSupportedException("NOT IMPLEMENTED: <reason>, see docs/dev-log.md")` and
   record it. No TODO stubs, no mock data, no hardcoded success, no fake
   "online" state.
2. **Layering is one-directional.**
   `UI → Agent Service → Tool Registry → Permission Manager → Tool Executor`.
   UI code must not call Windows APIs to do business work.
3. **UI must follow `design/`.** Colours, type scale, radii and spacing come only
   from `design/tokens.css`. Check `design/pixso-final/index.html` and
   `manifest.json` before writing a page, and cover **all** variants
   (Windows × light/dark, Android × light/dark).
4. **Async everywhere.** No `.Wait()`, `.Result` or `Thread.Sleep()` on a UI
   thread. Every long operation takes a `CancellationToken`.
5. **Never commit** API keys, tokens, private certificates, or real user data.

## Before you open a PR

```powershell
./scripts/build.ps1   # must pass with 0 warnings (TreatWarningsAsErrors)
./scripts/test.ps1    # all tests green
```

Then:

- add or update unit tests for the behaviour you changed
- append an entry to `docs/dev-log.md` (date, change, reason, test, known issues)
- update `CHANGELOG.md` under the unreleased section

## Commit messages

Short imperative subject, optional body explaining *why*. Reference the issue:

```
Add path validation to file tools

Rejects traversal, UNC and device paths before any file tool runs.
Fixes #42
```

## Adding things

- **A Tool** — implement `ITool` in `OpenAgent.Tools`, register it in
  `ToolRegistry`, give it a real risk level. See `docs/development.md`.
- **A Provider** — implement `IAgentProvider` in `OpenAgent.Providers`.
  Never hardcode a CLI's flags; detect capabilities from `--help`.
- **A protocol message** — add the DTO to `OpenAgent.Shared`, bump the schema
  version, keep N and N-1 compatible.

## Code style

C#: `nullable enable`, implicit usings, async suffix, analyzers on.
Kotlin: coroutines, sealed interfaces, `StateFlow`, immutable UI state.

## License

Contributions are licensed under Apache-2.0, same as the project.
