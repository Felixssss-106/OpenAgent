using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OpenAgent.Core;

namespace OpenAgent.Providers;

/// <summary>
/// A provider that drives a discovered external agent CLI (Codex, Claude Code,
/// OpenCode, Pi, Gemini). It is registered only when <see cref="CliDiscovery"/>
/// finds the executable on PATH, so installing a CLI makes it appear as a real
/// provider with no shell change (spec sections 24-27). The CLI's output is
/// captured and returned as steps; the host's approval flow still governs any
/// action the CLI itself takes.
/// </summary>
public sealed class CliAgentProvider : IAgentProvider
{
    private readonly CliInvocationProfile _profile;
    private readonly IProcessRunner _runner;

    public CliAgentProvider(CliInvocationProfile profile, IProcessRunner runner)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(runner);

        _profile = profile;
        _runner = runner;
    }

    public string Id => _profile.ProviderId;
    public string DisplayName => _profile.DisplayName;
    public AgentCapabilities Capabilities => _profile.Capabilities;

    public Task<AgentSession> CreateSessionAsync(string taskId, CancellationToken cancellationToken = default) =>
        Task.FromResult(new AgentSession(
            Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture), taskId, DateTimeOffset.UtcNow));

    public async Task<IReadOnlyList<ProviderStep>> SendPromptAsync(
        AgentSession session,
        string prompt,
        IReadOnlyList<ProviderToolInfo> tools,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var steps = new List<ProviderStep>
        {
            new(ProviderStepKind.Thought, $"通过 {DisplayName} 执行指令"),
            new(ProviderStepKind.ToolCall, Id, Id, "{\"prompt\":" + JsonSerializer.Serialize(prompt) + "}"),
        };

        ProcessRunResult result;
        try
        {
            result = await _runner.RunAsync(_profile.Executables[0], _profile.BuildArguments(prompt), cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            steps.Add(new ProviderStep(ProviderStepKind.Error, $"调用 {DisplayName} 失败: {ex.Message}"));
            return steps;
        }

        if (result.ExitCode != 0 && !string.IsNullOrWhiteSpace(result.StdErr))
        {
            steps.Add(new ProviderStep(ProviderStepKind.Error, result.StdErr.Trim()));
            return steps;
        }

        steps.Add(new ProviderStep(ProviderStepKind.ToolResult, (result.StdOut ?? string.Empty).Trim()));
        return steps;
    }

    // The runner is per-call and owns its process lifetime, so stopping a session
    // is a no-op here; a streaming adapter would track the running process and
    // cancel it (spec section 23).
    public Task StopAsync(AgentSession session, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    public Task ResumeAsync(AgentSession session, CancellationToken cancellationToken = default) =>
        Task.CompletedTask;

    // A real CLI adapter would re-verify the executable still exists; the profile
    // is static once discovered, so it reports healthy until proven otherwise.
    public Task<AgentHealth> HealthCheckAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(AgentHealth.Healthy);
}
