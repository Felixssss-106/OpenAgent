using OpenAgent.Core;

namespace OpenAgent.Security.Tests;

public sealed class PermissionServiceTests
{
    private static readonly PermissionRequest Safe = new("system.get_info", RiskLevel.Safe, true);
    private static readonly PermissionRequest Low = new("app.launch", RiskLevel.Low, false);
    private static readonly PermissionRequest Medium = new("file.move", RiskLevel.Medium, true);
    private static readonly PermissionRequest High = new("file.delete", RiskLevel.High, false);
    private static readonly PermissionRequest Critical = new("system.shutdown", RiskLevel.Critical, false);

    [Fact]
    public void Read_only_allows_queries_and_refuses_everything_else()
    {
        var service = new PermissionService();

        Assert.Equal(PermissionDecision.Allow, service.Evaluate(Safe, PermissionMode.ReadOnly));
        Assert.Equal(PermissionDecision.Deny, service.Evaluate(Medium, PermissionMode.ReadOnly));
        Assert.Equal(PermissionDecision.Deny, service.Evaluate(High, PermissionMode.ReadOnly));
    }

    [Fact]
    public void Read_only_refuses_an_irreversible_low_risk_call()
    {
        var service = new PermissionService();

        // app.launch is low risk but cannot be undone, so read-only must refuse.
        Assert.Equal(PermissionDecision.Deny, service.Evaluate(Low, PermissionMode.ReadOnly));

        // A screenshot is also low risk, but it is reversible, so it stays allowed.
        var screenshot = new PermissionRequest("screen.capture", RiskLevel.Low, true);
        Assert.Equal(PermissionDecision.Allow, service.Evaluate(screenshot, PermissionMode.ReadOnly));
    }

    [Fact]
    public void Read_only_never_asks_it_just_cannot_do_it()
    {
        var service = new PermissionService();

        Assert.Equal(PermissionDecision.Deny, service.Evaluate(Critical, PermissionMode.ReadOnly));
    }

    [Fact]
    public void Ask_before_actions_runs_safe_tools_and_prompts_for_the_rest()
    {
        var service = new PermissionService();

        Assert.Equal(PermissionDecision.Allow, service.Evaluate(Safe, PermissionMode.AskBeforeActions));
        Assert.Equal(PermissionDecision.RequireApproval, service.Evaluate(Low, PermissionMode.AskBeforeActions));
        Assert.Equal(PermissionDecision.RequireApproval, service.Evaluate(High, PermissionMode.AskBeforeActions));
    }

    [Fact]
    public void Auto_approve_still_stops_at_high_risk()
    {
        var service = new PermissionService();

        Assert.Equal(PermissionDecision.Allow, service.Evaluate(Low, PermissionMode.AutoApprove));
        Assert.Equal(PermissionDecision.Allow, service.Evaluate(Medium, PermissionMode.AutoApprove));
        Assert.Equal(PermissionDecision.RequireApproval, service.Evaluate(High, PermissionMode.AutoApprove));
        Assert.Equal(PermissionDecision.Deny, service.Evaluate(Critical, PermissionMode.AutoApprove));
    }

    [Fact]
    public void Full_access_still_confirms_critical_operations()
    {
        var service = new PermissionService();

        Assert.Equal(PermissionDecision.Allow, service.Evaluate(High, PermissionMode.FullAccess));
        Assert.Equal(PermissionDecision.RequireApproval, service.Evaluate(Critical, PermissionMode.FullAccess));
    }

    [Fact]
    public void Destructive_tools_are_confirmed_even_when_the_user_allow_lists_them()
    {
        var service = new PermissionService(new PermissionOptions
        {
            AutoApproveTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "file.delete" },
        });

        // file.delete is on the never-auto list, so the allow-list must not win.
        Assert.Equal(
            PermissionDecision.RequireApproval,
            service.Evaluate(High, PermissionMode.AskBeforeActions));
    }

    [Fact]
    public void A_user_allow_listed_low_risk_tool_runs_unattended()
    {
        var service = new PermissionService(new PermissionOptions
        {
            AutoApproveTools = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "app.launch" },
        });

        Assert.Equal(
            PermissionDecision.Allow,
            service.Evaluate(Low, PermissionMode.AskBeforeActions));
    }

    [Fact]
    public void Batch_approval_is_capped_at_medium_risk()
    {
        Assert.True(PermissionService.IsBatchApprovable(RiskLevel.Low));
        Assert.True(PermissionService.IsBatchApprovable(RiskLevel.Medium));
        Assert.False(PermissionService.IsBatchApprovable(RiskLevel.High));
        Assert.False(PermissionService.IsBatchApprovable(RiskLevel.Critical));
    }
}
