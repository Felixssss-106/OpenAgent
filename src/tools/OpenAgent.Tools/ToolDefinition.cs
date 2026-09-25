using OpenAgent.Core;

namespace OpenAgent.Tools;

/// <summary>
/// Everything the Agent, the permission engine and the UI need to know about a
/// tool before it is ever executed (spec section 125).
/// </summary>
public sealed class ToolDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>JSON Schema for the arguments object (spec section 127).</summary>
    public string InputSchemaJson { get; set; } = "{\"type\":\"object\",\"properties\":{}}";

    public RiskLevel Risk { get; set; } = RiskLevel.Medium;

    public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();

    /// <summary>False means the call cannot be undone and the approval card must say so.</summary>
    public bool Reversible { get; set; }

    public bool RequiresApproval { get; set; } = true;
}
