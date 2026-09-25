using OpenAgent.Core;

namespace OpenAgent.Security;

public enum PermissionDecision
{
    /// <summary>Run it now, no prompt.</summary>
    Allow = 0,

    /// <summary>Show an approval card and wait for the user.</summary>
    RequireApproval = 1,

    /// <summary>Refuse. Read-only mode denies, it does not ask.</summary>
    Deny = 2,
}

/// <summary>
/// What the permission engine needs to know about a call. Deliberately not the
/// full <c>ToolDefinition</c>, so Security never depends on the tool layer.
/// </summary>
public sealed record PermissionRequest(
    string ToolId,
    RiskLevel Risk,
    bool Reversible,
    string? TargetPath = null);
