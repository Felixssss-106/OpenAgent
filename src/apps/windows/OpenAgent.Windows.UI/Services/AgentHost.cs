using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using OpenAgent.Core.Domain;

namespace OpenAgent.Windows.UI.Services;

/// <summary>
/// Service locator for <see cref="IAgentHost"/>. Pages are created by XAML
/// navigation, so there is no constructor to inject through; the app registers
/// the real host once at startup and every page reads it from here.
/// </summary>
public static class AgentHost
{
    private static IAgentHost s_current = NullAgentHost.Instance;

    public static IAgentHost Current => s_current;

    public static void Register(IAgentHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        s_current = host;
    }

    /// <summary>True once the app has wired a real composition root.</summary>
    public static bool IsWired => s_current is not NullAgentHost;
}

/// <summary>
/// Keeps pages renderable before the composition root exists (and keeps tests
/// and the designer from needing a database): every query returns empty.
/// </summary>
public sealed class NullAgentHost : IAgentHost
{
    public static NullAgentHost Instance { get; } = new();

    private NullAgentHost()
    {
    }

    public Task<IReadOnlyList<AgentTask>> RecentTasksAsync(
        int limit = 50,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<AgentTask>>(Array.Empty<AgentTask>());

    public Task<IReadOnlyList<TaskEventRecord>> TaskEventsAsync(
        string taskId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<TaskEventRecord>>(Array.Empty<TaskEventRecord>());

    public Task<IReadOnlyList<ToolSummary>> ToolsAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ToolSummary>>(Array.Empty<ToolSummary>());

    public Task<IReadOnlyList<DeviceSummary>> DevicesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<DeviceSummary>>(Array.Empty<DeviceSummary>());
}
