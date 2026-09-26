using OpenAgent.Core;

namespace OpenAgent.Providers;

/// <summary>
/// Per-CLI invocation profiles, keyed by the id <see cref="CliDiscovery"/> reports
/// (spec sections 24-27). These are the documented minimal prompt-passing forms;
/// richer, version-specific behaviour belongs in each CLI's own adapter, written
/// when that CLI is detected on the host. External agents are autonomous and
/// high-impact, so every profile carries <see cref="AgentCapabilities.Approval"/>.
/// </summary>
public static class CliInvocationProfiles
{
    private static readonly Dictionary<string, CliInvocationProfile> Profiles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["codex"] = new(
            "openagent.cli.codex", "Codex", new[] { "codex", "codex.exe" },
            AgentCapabilities.ToolCalling | AgentCapabilities.Approval | AgentCapabilities.StructuredOutput | AgentCapabilities.JsonOutput,
            new[] { "exec" }),
        ["claude"] = new(
            "openagent.cli.claude", "Claude Code", new[] { "claude", "claude.exe" },
            AgentCapabilities.ToolCalling | AgentCapabilities.Approval | AgentCapabilities.StructuredOutput | AgentCapabilities.JsonOutput,
            new[] { "-p" }),
        ["opencode"] = new(
            "openagent.cli.opencode", "OpenCode", new[] { "opencode", "opencode.exe" },
            AgentCapabilities.ToolCalling | AgentCapabilities.Approval | AgentCapabilities.StructuredOutput,
            new[] { "run" }),
        ["pi"] = new(
            "openagent.cli.pi", "Pi", new[] { "pi", "pi.exe" },
            AgentCapabilities.ToolCalling | AgentCapabilities.Approval | AgentCapabilities.StructuredOutput,
            Array.Empty<string>()),
        ["gemini"] = new(
            "openagent.cli.gemini", "Gemini", new[] { "gemini", "gemini.exe" },
            AgentCapabilities.ToolCalling | AgentCapabilities.Approval | AgentCapabilities.StructuredOutput,
            Array.Empty<string>()),
    };

    /// <summary>Returns the profile for a discovered CLI id, or null if unknown.</summary>
    public static CliInvocationProfile? For(string id) =>
        Profiles.TryGetValue(id, out var profile) ? profile : null;
}
