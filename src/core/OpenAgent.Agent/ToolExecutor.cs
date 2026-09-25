using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAgent.Core;
using OpenAgent.Core.Domain;
using OpenAgent.Core.Error;
using OpenAgent.Core.Retry;
using OpenAgent.Core.Time;
using OpenAgent.Security;
using OpenAgent.Shared.Protocol;
using OpenAgent.Storage.Repositories;
using OpenAgent.Tools;

namespace OpenAgent.Agent;

public sealed record ToolExecutionRequest(
    string ToolId,
    JsonElement Arguments,
    string? TaskId = null,
    string? DeviceId = null,
    string? WorkingDirectory = null,
    PermissionMode PermissionMode = PermissionMode.AskBeforeActions,
    string ProviderId = "openagent.native",
    string? ApprovalId = null);

public sealed record ToolExecutionOutcome(
    ToolResult Result,
    PermissionDecision Decision,
    bool Executed,
    ApprovalRecord? PendingApproval);

/// <summary>
/// The gate every tool call passes: resolve → validate → permission → path
/// policy → execute → audit (spec sections 21, 39, 132).
/// </summary>
public sealed class ToolExecutor
{
    private readonly ToolRegistry _registry;
    private readonly PermissionService _permissions;
    private readonly AuditLogRepository _audit;
    private readonly IRetryPolicy _retry;
    private readonly IUtcClock _clock;
    private readonly ILogger<ToolExecutor> _logger;

    public ToolExecutor(
        ToolRegistry registry,
        PermissionService permissions,
        AuditLogRepository audit,
        IRetryPolicy retry,
        IUtcClock clock,
        ILogger<ToolExecutor>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(permissions);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(retry);
        ArgumentNullException.ThrowIfNull(clock);

        _registry = registry;
        _permissions = permissions;
        _audit = audit;
        _retry = retry;
        _clock = clock;
        _logger = logger ?? NullLogger<ToolExecutor>.Instance;
    }

    public async Task<ToolExecutionOutcome> ExecuteAsync(
        ToolExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tool = _registry.Get(request.ToolId);
        if (tool is null)
        {
            return Denied(ToolResult.Fail(ErrorCodes.ToolError(3), $"未知工具：{request.ToolId}"));
        }

        var validation = _registry.ValidateArguments(request.ToolId, request.Arguments);
        if (!validation.IsValid)
        {
            return Denied(ToolResult.Fail(ErrorCodes.ToolError(2), validation.Error ?? "参数无效"));
        }

        var decision = _permissions.Evaluate(
            new PermissionRequest(tool.Definition.Id, tool.Definition.Risk, tool.Definition.Reversible,
                TryReadPath(request.Arguments)),
            request.PermissionMode);

        if (decision == PermissionDecision.Deny)
        {
            return Denied(ToolResult.Fail(
                ErrorCodes.PermissionError(1),
                $"当前权限模式不允许执行 {tool.Definition.Id}"));
        }

        // A file-targeted call also has to clear the path policy, independently
        // of the risk-based decision.
        if (TryReadPath(request.Arguments) is { } path)
        {
            var pathCheck = PathPolicy.Validate(path);
            if (!pathCheck.IsAllowed)
            {
                return Denied(ToolResult.Fail(ErrorCodes.PermissionError(2), pathCheck.Describe()));
            }
        }

        if (decision == PermissionDecision.RequireApproval && request.ApprovalId is null)
        {
            return new ToolExecutionOutcome(
                ToolResult.Fail(ErrorCodes.PermissionError(3), "需要用户批准"),
                PermissionDecision.RequireApproval,
                false,
                null);
        }

        var context = new ToolContext
        {
            TaskId = request.TaskId,
            DeviceId = request.DeviceId,
            WorkingDirectory = request.WorkingDirectory,
            PermissionMode = request.PermissionMode,
        };

        var stopwatch = Stopwatch.StartNew();
        ToolResult result;
        try
        {
            result = await RunWithRetryAsync(tool, context, request.Arguments, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            var cancelled = ToolResult.Fail(ErrorCodes.AgentError(3), "任务已取消");
            WriteAudit(request, tool, cancelled, null, stopwatch.ElapsedMilliseconds);
            return Denied(cancelled);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Tool {ToolId} threw.", tool.Definition.Id);
            var failed = ToolResult.Fail(ErrorCodes.ToolError(9), ex.Message);
            WriteAudit(request, tool, failed, null, stopwatch.ElapsedMilliseconds);
            return Denied(failed);
        }

        stopwatch.Stop();
        WriteAudit(request, tool, result, request.ApprovalId, stopwatch.ElapsedMilliseconds);

        return new ToolExecutionOutcome(result, decision, true, null);
    }

    /// <summary>
    /// Only side-effect-free tools are retried. Re-running a tool that mutates
    /// state is exactly the duplicate-execution bug idempotency exists to prevent
    /// (spec sections 67, 200).
    /// </summary>
    private async Task<ToolResult> RunWithRetryAsync(
        ITool tool,
        ToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        var attempt = 1;
        while (true)
        {
            try
            {
                return await tool.ExecuteAsync(context, arguments, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (
                tool.Definition.Risk == RiskLevel.Safe &&
                _retry.Classify(ex, attempt) == RetryAction.Retry)
            {
                var delay = _retry.DelayFor(attempt);
                _logger.LogWarning(ex, "Tool {ToolId} failed transiently, retrying in {Delay}.", tool.Definition.Id, delay);
                await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                attempt++;
            }
        }
    }

    private static ToolExecutionOutcome Denied(ToolResult result) =>
        new(result, PermissionDecision.Deny, false, null);

    private void WriteAudit(
        ToolExecutionRequest request,
        ITool tool,
        ToolResult result,
        string? approvalId,
        long elapsedMs)
    {
        _audit.Insert(new AuditEntry
        {
            TimestampUtc = _clock.UtcNow,
            TaskId = request.TaskId,
            DeviceId = request.DeviceId,
            ProviderId = request.ProviderId,
            ToolId = tool.Definition.Id,
            Risk = tool.Definition.Risk,
            ArgumentsSummary = SecretRedactor.Summary(request.Arguments.ToString()),
            Success = result.Success,
            Result = result.Error?.Message,
            ApprovalId = approvalId,
            DurationMs = elapsedMs,
        });
    }

    private static string? TryReadPath(JsonElement arguments)
    {
        if (arguments.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var name in new[] { "path", "source", "target", "directory" })
        {
            if (arguments.TryGetProperty(name, out var node) && node.ValueKind == JsonValueKind.String)
            {
                return node.GetString();
            }
        }

        return null;
    }
}
