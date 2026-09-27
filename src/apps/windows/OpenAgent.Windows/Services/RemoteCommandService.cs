using System.Text.Json;
using Microsoft.Extensions.Logging;
using OpenAgent.Agent;
using OpenAgent.Core;
using OpenAgent.Core.Domain;
using OpenAgent.Core.Error;
using OpenAgent.Core.Tasks;
using OpenAgent.Security;
using OpenAgent.Shared.Protocol;
using OpenAgent.Transport;
using OpenAgent.Transport.Pairing;
using OpenAgent.Windows.UI.Services;

namespace OpenAgent.Windows.Services;

/// <summary>One pending remote approval, handed to the shell's UI gate.</summary>
public sealed record RemoteApprovalRequest(
    string TaskId,
    string ToolId,
    string DisplayName,
    RiskLevel Risk,
    bool Reversible,
    string ArgumentsSummary);

/// <summary>
/// Turns LAN command envelopes into real task runs: the phone's 打开记事本 takes
/// the same plan → execute → approve road a typed prompt does, and the result
/// envelope echoes the command's id so the phone can match it (docs/protocol.md
/// §3). Runs are serialized — the approval card is a single surface.
/// </summary>
internal sealed class RemoteCommandService
{
    private readonly AgentTaskService _tasks;
    private readonly ToolExecutor _executor;
    private readonly OpenAgentOptions _options;
    private readonly ITransport _transport;
    private readonly string _hostId;
    private readonly PairingService _pairing;
    private readonly Func<RemoteApprovalRequest, Task<(bool Approved, string? ApprovalId)>> _gate;
    private readonly ILogger<RemoteCommandService> _logger;
    private readonly SemaphoreSlim _runs = new(1, 1);

    public RemoteCommandService(
        AgentTaskService tasks,
        ToolExecutor executor,
        OpenAgentOptions options,
        ITransport transport,
        string hostId,
        PairingService pairing,
        Func<RemoteApprovalRequest, Task<(bool Approved, string? ApprovalId)>> gate,
        ILogger<RemoteCommandService> logger)
    {
        _tasks = tasks ?? throw new ArgumentNullException(nameof(tasks));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _hostId = hostId ?? throw new ArgumentNullException(nameof(hostId));
        _pairing = pairing ?? throw new ArgumentNullException(nameof(pairing));
        _gate = gate ?? throw new ArgumentNullException(nameof(gate));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// v2 policy: commands only run for paired peers (docs/protocol.md §4.4).
    /// The refusal is plaintext on purpose — the sender has no key yet.
    /// </summary>
    public Task RejectUnpairedAsync(LanMessageEnvelope command)
    {
        _logger.LogWarning("rejected unpaired command from {From}", command.From);
        Reply(command, "未配对：请先在两端完成配对", encrypted: false);
        return Task.CompletedTask;
    }

    public async Task HandleAsync(LanMessageEnvelope command)
    {
        if (command.Type != LanMessageType.Command)
        {
            return;
        }

        await _runs.WaitAsync();
        try
        {
            await RunAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "remote command from {From} failed", command.From);
            Reply(command, $"处理失败：{ex.Message}");
        }
        finally
        {
            _runs.Release();
        }
    }

    private async Task RunAsync(LanMessageEnvelope command)
    {
        var tools = await AgentHost.Current.ToolsAsync();
        var plan = CommandPlanner.Plan(command.Text, tools);
        if (plan is null)
        {
            // The planner is a keyword router, not a model: free text that maps
            // to no tool is answered honestly instead of guessed.
            _logger.LogInformation("remote command from {From} matched no tool: {Text}", command.From, command.Text);
            Reply(command, "未识别可执行的指令，试试“打开记事本”或“查看系统信息”");
            return;
        }

        _logger.LogInformation(
            "remote command from {From}: {ToolId} {Args}",
            command.From, plan.ToolId, plan.ArgumentsJson);

        var task = await _tasks.CreateAsync(command.Text, "openagent.native");
        await _tasks.AppendEventAsync(task.Id, TaskEventKinds.Assistant, command.Text);
        await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Running);

        using var document = JsonDocument.Parse(plan.ArgumentsJson);
        var request = new ToolExecutionRequest(
            plan.ToolId,
            document.RootElement.Clone(),
            task.Id,
            PermissionMode: _options.PermissionMode,
            ProviderId: "openagent.native");

        var outcome = await _executor.ExecuteAsync(request, _tasks.TokenFor(task.Id));

        if (!outcome.Executed && outcome.Decision == PermissionDecision.RequireApproval)
        {
            await _tasks.TransitionAsync(task.Id, AgentTaskStatus.WaitingApproval);
            var tool = tools.FirstOrDefault(
                candidate => string.Equals(candidate.Id, plan.ToolId, StringComparison.OrdinalIgnoreCase));
            var gate = await _gate(new RemoteApprovalRequest(
                task.Id,
                plan.ToolId,
                tool?.DisplayName ?? plan.ToolId,
                tool?.Risk ?? RiskLevel.Medium,
                tool?.Reversible ?? false,
                plan.ArgumentsJson));

            if (!gate.Approved)
            {
                var denied = $"{ErrorCodes.PermissionError(1)} 未获批准，操作已终止";
                await _tasks.AppendEventAsync(task.Id, TaskEventKinds.Error, denied);
                await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Failed, denied);
                _tasks.Release(task.Id);
                Reply(command, denied);
                return;
            }

            await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Running);
            outcome = await _executor.ExecuteAsync(
                request with { ApprovalId = gate.ApprovalId },
                _tasks.TokenFor(task.Id));
        }

        string replyText;
        if (outcome.Result.Success)
        {
            var summary = PlanViewMapper.DescribeResult(outcome.Result.Data?.GetRawText());
            await _tasks.AppendEventAsync(task.Id, TaskEventKinds.Tool, $"{plan.ToolId} → {summary}");
            await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Completed);
            replyText = summary;
        }
        else
        {
            var code = outcome.Result.Error?.Code ?? ErrorCodes.ToolError(9);
            var message = outcome.Result.Error?.Message ?? "工具执行失败";
            await _tasks.AppendEventAsync(task.Id, TaskEventKinds.Error, $"{code} {message}");
            await _tasks.TransitionAsync(task.Id, AgentTaskStatus.Failed, $"{code} {message}");
            replyText = $"{code} {message}";
        }

        _tasks.Release(task.Id);
        Reply(command, replyText);
    }

    private void Reply(LanMessageEnvelope command, string text) =>
        Reply(command, text, encrypted: _pairing.KeyFor(command.From) is not null);

    private void Reply(LanMessageEnvelope command, string text, bool encrypted)
    {
        // The result echoes the command's id (protocol §3) so the phone can pair
        // the answer with the request it sent; sealed when the pairing key exists.
        var reply = new LanMessageEnvelope(
            LanMessageType.Result,
            command.Id,
            _hostId,
            command.From,
            text,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var key = encrypted ? _pairing.KeyFor(command.From) : null;
        var payload = key is null ? reply : EnvelopeCrypto.Seal(reply, key);
        _ = _transport.SendAsync(command.From, payload.Encode());
    }
}
