using OpenAgent.Core.Tasks;

namespace OpenAgent.Core.Events;

/// <summary>
/// Event payloads (spec section 164). Deliberately small: the UI reads these,
/// richer detail is fetched from repositories on demand.
/// </summary>
public sealed record TaskStarted(string TaskId, string Prompt, string ProviderId, DateTimeOffset StartedAt);

public sealed record TaskProgress(string TaskId, string Step, int Completed, int Total);

public sealed record TaskStatusChanged(string TaskId, AgentTaskStatus From, AgentTaskStatus To);

public sealed record ApprovalRequested(
    string ApprovalId,
    string TaskId,
    string ToolId,
    RiskLevel Risk,
    bool Reversible,
    string Message,
    DateTimeOffset ExpiresAt);

public sealed record ApprovalResolved(string ApprovalId, string TaskId, bool Approved, string ResolvedBy);

public sealed record DeviceConnected(string DeviceId, string DeviceName, string DeviceType);

public sealed record DeviceDisconnected(string DeviceId, string Reason);

public sealed record FileTransferProgress(string TransferId, string FileName, long BytesMoved, long TotalBytes);

public sealed record ProviderStatusChanged(string ProviderId, string Status, string? Detail);
