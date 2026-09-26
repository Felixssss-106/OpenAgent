using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenAgent.Agent;
using OpenAgent.Core.Domain;
using OpenAgent.Tools;
using OpenAgent.Windows.UI.Services;

namespace OpenAgent.Windows.Services;

/// <summary>
/// Implements the UI's <see cref="IAgentHost"/> on top of the real composition
/// root. This is the only place in the shell that resolves agent services, and
/// it lives in the app project precisely so the UI library never has to
/// reference it (spec sections 165, 132).
/// </summary>
internal sealed class AgentHostAdapter : IAgentHost
{
    private readonly AgentTaskService _tasks;
    private readonly ToolRegistry _registry;

    public AgentHostAdapter(AgentTaskService tasks, ToolRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(registry);

        _tasks = tasks;
        _registry = registry;
    }

    public Task<IReadOnlyList<AgentTask>> RecentTasksAsync(
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        _tasks.RecentAsync(limit);

    public Task<IReadOnlyList<TaskEventRecord>> TaskEventsAsync(
        string taskId,
        CancellationToken cancellationToken = default) =>
        _tasks.EventsAsync(taskId);

    public Task<IReadOnlyList<ToolSummary>> ToolsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ToolSummary>>(
            _registry.All()
                .Select(definition => new ToolSummary(
                    definition.Id,
                    definition.Name,
                    definition.Description,
                    definition.Risk,
                    definition.Reversible,
                    definition.Permissions))
                .ToArray());
}
