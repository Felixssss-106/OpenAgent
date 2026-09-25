namespace OpenAgent.Core.Tasks;

/// <summary>
/// The only place that decides which state changes are legal. Callers must go
/// through <see cref="CanTransition"/>; the agent service throws rather than
/// silently corrupting a task.
/// </summary>
public static class TaskStateMachine
{
    private static readonly Dictionary<AgentTaskStatus, AgentTaskStatus[]> Allowed = new()
    {
        [AgentTaskStatus.Queued] = new[] { AgentTaskStatus.Planning, AgentTaskStatus.Running, AgentTaskStatus.Cancelled, AgentTaskStatus.Failed },
        [AgentTaskStatus.Planning] = new[] { AgentTaskStatus.WaitingApproval, AgentTaskStatus.Running, AgentTaskStatus.Cancelled, AgentTaskStatus.Failed },
        [AgentTaskStatus.WaitingApproval] = new[] { AgentTaskStatus.Running, AgentTaskStatus.Cancelled, AgentTaskStatus.Failed },
        [AgentTaskStatus.Running] = new[]
        {
            AgentTaskStatus.WaitingApproval,
            AgentTaskStatus.Paused,
            AgentTaskStatus.Completed,
            AgentTaskStatus.Failed,
            AgentTaskStatus.Cancelled,
        },
        [AgentTaskStatus.Paused] = new[] { AgentTaskStatus.Running, AgentTaskStatus.Cancelled, AgentTaskStatus.Failed },
        [AgentTaskStatus.Completed] = Array.Empty<AgentTaskStatus>(),
        [AgentTaskStatus.Failed] = Array.Empty<AgentTaskStatus>(),
        [AgentTaskStatus.Cancelled] = Array.Empty<AgentTaskStatus>(),
    };

    public static bool CanTransition(AgentTaskStatus from, AgentTaskStatus to)
    {
        if (from == to)
        {
            return false;
        }

        return Allowed.TryGetValue(from, out var next) && Array.IndexOf(next, to) >= 0;
    }

    public static IReadOnlyCollection<AgentTaskStatus> NextStates(AgentTaskStatus from) =>
        Allowed.TryGetValue(from, out var next) ? next : Array.Empty<AgentTaskStatus>();
}
