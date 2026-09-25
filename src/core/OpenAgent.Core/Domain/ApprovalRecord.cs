namespace OpenAgent.Core.Domain;

/// <summary>
/// Approval status (spec section 98).
/// </summary>
public enum ApprovalStatus
{
    Pending = 0,
    Approved = 1,
    Denied = 2,
    Expired = 3,
}

public sealed class ApprovalRecord
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string TaskId { get; set; } = string.Empty;

    public string ToolId { get; set; } = string.Empty;

    public RiskLevel Risk { get; set; } = RiskLevel.Medium;

    public bool Reversible { get; set; }

    public string Message { get; set; } = string.Empty;

    /// <summary>Arguments with secrets redacted (spec section 100).</summary>
    public string ArgumentsSummary { get; set; } = string.Empty;

    public string? AffectedPaths { get; set; }

    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public DateTimeOffset? ResolvedAtUtc { get; set; }

    public string? ResolvedBy { get; set; }

    public bool IsPending(DateTimeOffset now) =>
        Status == ApprovalStatus.Pending && now < ExpiresAtUtc;
}
