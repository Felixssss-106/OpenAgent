using OpenAgent.Core;

namespace OpenAgent.Providers;

/// <summary>
/// The known, transparent way to hand a single prompt to one external agent CLI.
/// Discovery (<see cref="CliDiscovery"/>) finds the executable on PATH; this
/// profile is the minimal prompt-passing form for that CLI. Per-CLI prefixes are
/// the documented, deliberate config of each adapter (spec section 27 — "each
/// CLI's own adapter, written when its CLI is actually present"), not behaviour
/// guessed or scattered through the shell. The prompt is always the final, quoted
/// argument.
/// </summary>
public sealed record CliInvocationProfile(
    /// <summary>Provider id, e.g. "openagent.cli.claude".</summary>
    string ProviderId,
    /// <summary>Human-facing name, e.g. "Claude Code".</summary>
    string DisplayName,
    /// <summary>Candidate executable names (mirrors <see cref="CliDiscovery.KnownClis"/>).</summary>
    string[] Executables,
    /// <summary>What this CLI can do, as reported to the host.</summary>
    AgentCapabilities Capabilities,
    /// <summary>Arguments placed before the prompt (e.g. ["-p"]).</summary>
    string[] PrefixArgs)
{
    /// <summary>Builds the argument string: prefix args, then the quoted prompt (with " escaped).</summary>
    public string BuildArguments(string prompt)
    {
        var prefix = PrefixArgs.Length == 0
            ? string.Empty
            : string.Join(" ", PrefixArgs) + " ";
        return prefix + "\"" + (prompt ?? string.Empty).Replace("\"", "\\\"") + "\"";
    }
}
