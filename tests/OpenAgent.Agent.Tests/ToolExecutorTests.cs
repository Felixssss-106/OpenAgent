using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OpenAgent.Agent;
using OpenAgent.Core;
using OpenAgent.Security;
using OpenAgent.Tools;

namespace OpenAgent.Agent.Tests;

/// <summary>
/// The permission gate is the security boundary, so these tests assert what is
/// refused at least as much as what runs.
/// </summary>
public sealed class ToolExecutorTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openagent-tests", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _provider;

    public ToolExecutorTests()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddOpenAgent(options => options.DatabaseRoot = _root);
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Ignore locked temp files.
        }
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public async Task A_safe_tool_runs_and_is_written_to_the_audit_log()
    {
        var executor = _provider.GetRequiredService<ToolExecutor>();
        var audit = _provider.GetRequiredService<OpenAgent.Storage.Repositories.AuditLogRepository>();

        var outcome = await executor.ExecuteAsync(new ToolExecutionRequest(
            "system.get_info",
            Args("{}"),
            PermissionMode: PermissionMode.FullAccess));

        Assert.True(outcome.Executed);
        Assert.True(outcome.Result.Success);
        Assert.Contains(audit.Recent(), e => e.ToolId == "system.get_info" && e.Success);
    }

    [Fact]
    public async Task A_low_risk_tool_stops_for_approval_by_default()
    {
        var executor = _provider.GetRequiredService<ToolExecutor>();

        var outcome = await executor.ExecuteAsync(new ToolExecutionRequest(
            "app.launch",
            Args("{\"target\":\"notepad\"}"),
            PermissionMode: PermissionMode.AskBeforeActions));

        Assert.False(outcome.Executed);
        Assert.Equal(PermissionDecision.RequireApproval, outcome.Decision);
        Assert.False(outcome.Result.Success);
    }

    [Fact]
    public async Task Read_only_simply_refuses_a_low_risk_side_effect()
    {
        var executor = _provider.GetRequiredService<ToolExecutor>();

        var outcome = await executor.ExecuteAsync(new ToolExecutionRequest(
            "app.launch",
            Args("{\"target\":\"notepad\"}"),
            PermissionMode: PermissionMode.ReadOnly));

        Assert.False(outcome.Executed);
        Assert.Equal(PermissionDecision.Deny, outcome.Decision);
    }

    [Fact]
    public async Task An_unknown_tool_is_refused()
    {
        var executor = _provider.GetRequiredService<ToolExecutor>();

        var outcome = await executor.ExecuteAsync(new ToolExecutionRequest(
            "file.delete",
            Args("{}"),
            PermissionMode: PermissionMode.FullAccess));

        Assert.False(outcome.Executed);
        Assert.False(outcome.Result.Success);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Result.Error!.Code));
    }

    [Fact]
    public async Task Arguments_are_validated_before_anything_else()
    {
        var executor = _provider.GetRequiredService<ToolExecutor>();

        var outcome = await executor.ExecuteAsync(new ToolExecutionRequest(
            "app.launch",
            Args("{}"),
            PermissionMode: PermissionMode.FullAccess));

        Assert.False(outcome.Executed);
        Assert.Contains("target", outcome.Result.Error!.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_approved_call_then_executes()
    {
        var executor = _provider.GetRequiredService<ToolExecutor>();
        var approvals = _provider.GetRequiredService<ApprovalService>();
        var taskService = _provider.GetRequiredService<AgentTaskService>();
        var task = await taskService.CreateAsync("打开记事本", "openagent.native");

        var approval = await approvals.RequestAsync(
            task.Id, "app.launch", RiskLevel.Low, false, "启动应用", "{}");

        Assert.Single(approvals.Pending());

        // "notepad" is not a real target here, so the call still fails — but it
        // is allowed to *run*, which is what this test is about.
        var before = await executor.ExecuteAsync(new ToolExecutionRequest(
            "app.launch", Args("{\"target\":\"notepad\"}"), task.Id,
            PermissionMode: PermissionMode.AskBeforeActions, ApprovalId: approval.Id));

        Assert.True(before.Executed);
        Assert.Equal(PermissionDecision.RequireApproval, before.Decision);
        Assert.False(before.Result.Success);
    }

    [Fact]
    public async Task An_expired_approval_cannot_be_resolved()
    {
        var clock = new TestClock();
        var approvals = new ApprovalService(
            _provider.GetRequiredService<OpenAgent.Storage.Repositories.ApprovalRepository>(),
            _provider.GetRequiredService<OpenAgent.Core.Events.IEventBus>(),
            clock,
            lifetime: TimeSpan.FromMinutes(1));

        var approval = await approvals.RequestAsync("t1", "file.move", RiskLevel.Medium, true, "移动", "{}");
        clock.Advance(TimeSpan.FromMinutes(2));

        Assert.False(await approvals.ResolveAsync(approval.Id, true, "user"));
        Assert.Equal(1, approvals.ExpireStale());
    }

    [Fact]
    public async Task An_approval_is_resolved_exactly_once()
    {
        var approvals = _provider.GetRequiredService<ApprovalService>();

        var approval = await approvals.RequestAsync("t1", "file.move", RiskLevel.Medium, true, "移动", "{}");

        Assert.True(await approvals.ResolveAsync(approval.Id, true, "user"));
        Assert.False(await approvals.ResolveAsync(approval.Id, false, "user"));
    }
}
