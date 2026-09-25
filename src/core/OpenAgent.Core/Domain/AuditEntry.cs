namespace OpenAgent.Core.Domain;

/// <summary>
/// One sensitive operation, permanently recorded (spec section 100).
/// Arguments are stored already redacted by the caller.
/// </summary>
public sealed class AuditEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateTimeOffset TimestampUtc { get; set; }

    public string? TaskId { get; set; }

    public string? DeviceId { get; set; }

    public string ProviderId { get; set; } = string.Empty;

    public string ToolId { get; set; } = string.Empty;

    public RiskLevel Risk { get; set; }

    public string ArgumentsSummary { get; set; } = string.Empty;

    public bool Success { get; set; }

    public string? Result { get; set; }

    public string? ApprovalId { get; set; }

    public long DurationMs { get; set; }
}
