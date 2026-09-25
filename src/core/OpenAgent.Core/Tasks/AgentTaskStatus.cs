namespace OpenAgent.Core.Tasks;

/// <summary>
/// Agent session / task states (spec section 35).
/// </summary>
public enum AgentTaskStatus
{
    Queued = 0,
    Planning = 1,
    WaitingApproval = 2,
    Running = 3,
    Paused = 4,
    Completed = 5,
    Failed = 6,
    Cancelled = 7,
}

public static class AgentTaskStatuses
{
    public static bool IsTerminal(AgentTaskStatus status) => status
        is AgentTaskStatus.Completed
        or AgentTaskStatus.Failed
        or AgentTaskStatus.Cancelled;
}
