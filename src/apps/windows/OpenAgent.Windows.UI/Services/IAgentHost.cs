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

    /// <summary>Devices currently visible to the transport.</summary>
    Task<IReadOnlyList<DeviceSummary>> DevicesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>Every agent the host can actually drive, default first.</summary>
    Task<IReadOnlyList<ProviderSummary>> ProvidersAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// The active permission mode, carried as the <c>OpenAgent.Security</c> enum
    /// name ("AskBeforeActions" …) because the UI library does not reference that
    /// project. Assigning applies to every later request — no restart needed —
    /// and unknown values fall back to the install default.
    /// </summary>
    string PermissionMode { get; set; }

    /// <summary>
    /// Ask the shell to surface the Command Center with <paramref name="prompt"/>
    /// already typed. Nothing executes here — the user still presses send — so a
    /// command written on any page stays under their control instead of being
    /// discarded by a button with nowhere to go.
    /// </summary>
    void RequestCommandCenter(string prompt);
}

/// <summary>
/// Device info the UI is allowed to see — mirrors <c>OpenAgent.Transport.DeviceRecord</c>
/// without the UI library referencing Transport directly (spec section 165). The
/// UI library depends only on Core and Shared; the device payload arrives through
/// this record via <see cref="IAgentHost"/>.
/// </summary>
public sealed record DeviceSummary(
    /// <summary>Display name (Environment.MachineName for the local host).</summary>
    string Name,
    /// <summary>"本机" / "在线" / "离线".</summary>
    string Tag,
    /// <summary>"Windows 10.0.xxx · loopback" style line.</summary>
    string SystemInfo,
    /// <summary>Optional live metrics; may be empty for devices that report none.</summary>
    string Metrics);

/// <summary>
/// One agent the host can drive. <paramref name="Kind"/> is the quiet second line
/// under the name — "内置" for the bundled agent, the CLI command for a discovered
/// one — so the page never claims a provider is something it is not.
/// </summary>
public sealed record ProviderSummary(
    string Id,
    string DisplayName,
    string Kind,
    bool IsDefault);
