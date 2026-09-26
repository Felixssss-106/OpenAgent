using System.Threading;
using System.Threading.Tasks;
using OpenAgent.Core.Domain;

namespace OpenAgent.Windows.UI.Services;

/// <summary>
/// The whole surface the view layer needs from the agent. It is declared here,
/// in the UI library, and implemented by the app project — that direction of
/// dependency is what keeps <c>OpenAgent.Windows.UI</c> from referencing the app
/// (or Storage, or the tool executor) and closing a cycle (spec section 165).
/// </summary>
public interface IAgentHost
{
    /// <summary>Most recent tasks first.</summary>
    Task<IReadOnlyList<AgentTask>> RecentTasksAsync(
        int limit = 50,
        CancellationToken cancellationToken = default);

    /// <summary>The output stream of one task, oldest first.</summary>
    Task<IReadOnlyList<TaskEventRecord>> TaskEventsAsync(
        string taskId,
        CancellationToken cancellationToken = default);

    /// <summary>Everything currently registered in the tool catalogue.</summary>
    Task<IReadOnlyList<ToolSummary>> ToolsAsync(
        CancellationToken cancellationToken = default);
}
