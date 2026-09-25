using OpenAgent.Core;

namespace OpenAgent.Security;

/// <summary>
/// The single decision point for "may this call happen, and does the user have
/// to confirm it first?" (spec sections 17, 239, 240).
/// </summary>
public sealed class PermissionService
{
    private readonly PermissionOptions _options;

    public PermissionService()
        : this(new PermissionOptions())
    {
    }

    public PermissionService(PermissionOptions options)
    {
        _options = options;
    }

    public PermissionDecision Evaluate(PermissionRequest request, PermissionMode mode)
    {
        ArgumentNullException.ThrowIfNull(request);

        var blocked = _options.NeverAutoApproveTools.Contains(request.ToolId);

        return mode switch
        {
            // Read-only never asks — it simply cannot do anything destructive.
            // A low-risk call is only allowed when it has no lasting effect
            // (a screenshot is fine, launching an application is not).
            PermissionMode.ReadOnly =>
                request.Risk == RiskLevel.Safe || (request.Risk == RiskLevel.Low && request.Reversible)
                    ? PermissionDecision.Allow
                    : PermissionDecision.Deny,

            PermissionMode.AskBeforeActions => EvaluateAsk(request, blocked),

            PermissionMode.AutoApprove => EvaluateAutoApprove(request, blocked),

            PermissionMode.FullAccess => request.Risk >= RiskLevel.Critical
                ? PermissionDecision.RequireApproval
                : PermissionDecision.Allow,

            _ => PermissionDecision.Deny,
        };
    }

    /// <summary>
    /// Batch approval is only offered for operations at or below
    /// <see cref="RiskLevels.BatchApprovalCeiling"/> (spec section 99).
    /// </summary>
    public static bool IsBatchApprovable(RiskLevel risk) => risk <= RiskLevels.BatchApprovalCeiling;

    private PermissionDecision EvaluateAsk(PermissionRequest request, bool blocked)
    {
        if (blocked)
        {
            return PermissionDecision.RequireApproval;
        }

        if (request.Risk == RiskLevel.Safe)
        {
            return PermissionDecision.Allow;
        }

        // Only low-risk tools can be put on the user's auto-approve list.
        return request.Risk <= RiskLevel.Low && _options.AutoApproveTools.Contains(request.ToolId)
            ? PermissionDecision.Allow
            : PermissionDecision.RequireApproval;
    }

    private static PermissionDecision EvaluateAutoApprove(PermissionRequest request, bool blocked)
    {
        // Critical stays denied even in auto-approve mode — the user opted into
        // convenience, not into irreversible system changes.
        if (request.Risk >= RiskLevel.Critical)
        {
            return PermissionDecision.Deny;
        }

        if (blocked)
        {
            return PermissionDecision.RequireApproval;
        }

        return request.Risk switch
        {
            <= RiskLevel.Medium => PermissionDecision.Allow,
            RiskLevel.High => PermissionDecision.RequireApproval,
            _ => PermissionDecision.Deny,
        };
    }
}

public sealed class PermissionOptions
{
    /// <summary>Tool ids the user has explicitly allowed to run unattended.</summary>
    public IReadOnlySet<string> AutoApproveTools { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Destructive tools that always require confirmation regardless of mode
    /// (spec section 42): delete, terminate, shutdown, raw shell.
    /// </summary>
    public IReadOnlySet<string> NeverAutoApproveTools { get; init; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "file.delete",
            "process.terminate",
            "process.stop",
            "system.shutdown",
            "system.restart",
            "shell.execute",
        };
}
