using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using OpenAgent.Agent;
using OpenAgent.Core.Domain;
using OpenAgent.Providers;
using OpenAgent.Security;
using OpenAgent.Tools;
using OpenAgent.Transport;
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
    private readonly ITransport _transport;
    private readonly ProviderRegistry _providers;
    private readonly OpenAgentOptions _options;

    public AgentHostAdapter(
        AgentTaskService tasks,
        ToolRegistry registry,
        ITransport transport,
        ProviderRegistry providers,
        OpenAgentOptions options)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(providers);
        ArgumentNullException.ThrowIfNull(options);

        _tasks = tasks;
        _registry = registry;
        _transport = transport;
        _providers = providers;
        _options = options;
    }

    /// <summary>
    /// The mode crosses as the enum's own name. Assignment is live: every later
    /// ToolExecutionRequest reads it from the same options instance, so the
    /// settings pick needs no restart. Unknown strings fall back to the install
    /// default, never to something more permissive. Explicitly implemented so the
    /// member name does not shadow the <c>PermissionMode</c> type inside the class.
    /// </summary>
    string IAgentHost.PermissionMode
    {
        get => _options.PermissionMode.ToString();
        set => _options.PermissionMode = Enum.TryParse<PermissionMode>(value, out var mode)
            ? mode
            : PermissionMode.AskBeforeActions;
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

    public async Task<IReadOnlyList<DeviceSummary>> DevicesAsync(
        CancellationToken cancellationToken = default)
    {
        var devices = await _transport.DiscoverAsync(cancellationToken);
        return devices
            .Select(device => new DeviceSummary(
                Name: device.Name,
                Tag: device.ConnectionType switch
                {
                    "loopback" => "本机",
                    "lan" => "局域网",
                    _ => device.IsOnline ? "在线" : "离线",
                },
                SystemInfo: $"{device.Platform} · {device.Version} · {device.ConnectionType}",
                Metrics: device.Metrics ?? string.Empty))
            .ToArray();
    }

    public void RequestCommandCenter(string prompt) =>
        ((App)Microsoft.UI.Xaml.Application.Current).ShowCommandCenter(prompt);

    /// <summary>
    /// The default agent leads, then everything discovered on PATH. Registration
    /// is the claim being made here — a provider appears in this list only because
    /// the host really can hand it a prompt.
    /// </summary>
    public Task<IReadOnlyList<ProviderSummary>> ProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        var defaultId = ProviderRegistry.DefaultProviderId;
        return Task.FromResult<IReadOnlyList<ProviderSummary>>(
            _providers.All
                .Select(provider => new ProviderSummary(
                    provider.Id,
                    provider.DisplayName,
                    provider.Id == defaultId ? "内置 · 规则引擎" : "CLI · " + provider.Id,
                    provider.Id == defaultId))
                .OrderBy(provider => provider.IsDefault ? 0 : 1)
                .ThenBy(provider => provider.DisplayName, StringComparer.CurrentCultureIgnoreCase)
                .ToArray());
    }
}
