using OpenAgent.Core;

namespace OpenAgent.Windows.UI.Services;

/// <summary>
/// The tool metadata the UI is allowed to see. Mirrors
/// <c>OpenAgent.Tools.ToolDefinition</c> without making the view layer depend on
/// the tool assemblies (spec sections 125, 165): the UI library only references
/// Core and Shared, everything else arrives through <see cref="IAgentHost"/>.
/// </summary>
public sealed record ToolSummary(
    string Id,
    string Name,
    string Description,
    RiskLevel Risk,
    bool Reversible,
    IReadOnlyList<string> Permissions)
{
    /// <summary>Localised, human-facing name; falls back to the tool id.</summary>
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? Id : Name;
}
