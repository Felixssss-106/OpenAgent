using OpenAgent.Core.Tasks;

namespace OpenAgent.Core.Tests;

public sealed class AgentTaskStateMachineTests
{
    [Theory]
    [InlineData(AgentTaskStatus.Queued, AgentTaskStatus.Planning)]
    [InlineData(AgentTaskStatus.Planning, AgentTaskStatus.WaitingApproval)]
    [InlineData(AgentTaskStatus.WaitingApproval, AgentTaskStatus.Running)]
    [InlineData(AgentTaskStatus.Running, AgentTaskStatus.Paused)]
    [InlineData(AgentTaskStatus.Paused, AgentTaskStatus.Running)]
    [InlineData(AgentTaskStatus.Running, AgentTaskStatus.Completed)]
    [InlineData(AgentTaskStatus.Running, AgentTaskStatus.Failed)]
    [InlineData(AgentTaskStatus.Running, AgentTaskStatus.Cancelled)]
    public void Legal_transitions_are_allowed(AgentTaskStatus from, AgentTaskStatus to) =>
        Assert.True(TaskStateMachine.CanTransition(from, to), $"{from} → {to} should be allowed");

    [Theory]
    [InlineData(AgentTaskStatus.Completed, AgentTaskStatus.Running)]
    [InlineData(AgentTaskStatus.Cancelled, AgentTaskStatus.Running)]
    [InlineData(AgentTaskStatus.Failed, AgentTaskStatus.Running)]
    [InlineData(AgentTaskStatus.Queued, AgentTaskStatus.Completed)]
    public void Illegal_transitions_are_rejected(AgentTaskStatus from, AgentTaskStatus to) =>
        Assert.False(TaskStateMachine.CanTransition(from, to), $"{from} → {to} should be rejected");

    [Fact]
    public void Terminal_states_have_no_way_out()
    {
        foreach (var status in new[]
                 {
                     AgentTaskStatus.Completed,
                     AgentTaskStatus.Failed,
                     AgentTaskStatus.Cancelled,
                 })
        {
            Assert.True(AgentTaskStatuses.IsTerminal(status));
            Assert.Empty(TaskStateMachine.NextStates(status));
        }
    }

    [Fact]
    public void A_state_is_never_a_transition_to_itself()
    {
        foreach (var status in Enum.GetValues<AgentTaskStatus>())
        {
            Assert.False(TaskStateMachine.CanTransition(status, status));
        }
    }
}
