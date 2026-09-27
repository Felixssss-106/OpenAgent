using System.Collections.Generic;
using OpenAgent.Core;

namespace OpenAgent.Providers;

/// <summary>
/// The known, transparent way to hand a single prompt to one external agent CLI.
/// Discovery (<see cref="CliDiscovery"/>) finds the executable on PATH; this
/// profile is the minimal prompt-passing form for that CLI. Per-CLI prefixes are
/// the documented, deliberate config of each adapter (spec section 27 — "each
/// CLI's own adapter, written when its CLI is actually present"), not behaviour
/// guessed or scattered through the shell. The prompt is always the final argv
/// entry, passed as its own list element.
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
    /// <summary>
    /// Builds the argv list: prefix args, then the prompt as one literal final
    /// argument. The OS joins the list, so no quoting layer exists for a prompt
    /// to break out of — a prompt can never add argv items to the CLI's command
    /// line, no matter what quotes or backslashes it contains.
    /// </summary>
    public IReadOnlyList<string> BuildArgumentList(string prompt) =>
        new List<string>(PrefixArgs) { prompt ?? string.Empty };
}
