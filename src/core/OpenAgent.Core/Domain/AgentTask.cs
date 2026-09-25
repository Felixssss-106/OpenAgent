using OpenAgent.Core.Tasks;

namespace OpenAgent.Core.Domain;

/// <summary>
/// One user request handled by one provider (spec section 35).
/// Timestamps are always UTC; the UI converts (spec section 64).
/// </summary>
public sealed class AgentTask
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Prompt { get; set; } = string.Empty;

    public string ProviderId { get; set; } = string.Empty;

    public AgentTaskStatus Status { get; set; } = AgentTaskStatus.Queued;

    public string? WorkingDirectory { get; set; }

    public string? Summary { get; set; }

    public string? Error { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }
}
