using OpenAgent.Core;
using OpenAgent.Windows.UI.Services;
using Xunit;

namespace OpenAgent.Windows.UI.Tests;

public sealed class CommandPlannerTests
{
    private static IReadOnlyList<ToolSummary> Tools() => new[]
    {
        new ToolSummary("app.launch", "Launch App", "Launch an application", RiskLevel.Low, false, Array.Empty<string>()),
        new ToolSummary("process.list", "Process List", "List running processes", RiskLevel.Safe, true, Array.Empty<string>()),
        new ToolSummary("system.get_info", "System Info", "Read system info", RiskLevel.Safe, true, Array.Empty<string>()),
    };

    [Fact]
    public void Plan_routes_launch_keyword()
    {
        var plan = CommandPlanner.Plan("打开 notepad", Tools());
        Assert.NotNull(plan);
        Assert.Contains("app.launch", plan!.ToolId);
        Assert.Contains("notepad", plan.ArgumentsJson);
    }

    [Fact]
    public void Plan_routes_process_keyword()
    {
        var plan = CommandPlanner.Plan("查看进程", Tools());
        Assert.NotNull(plan);
        Assert.Contains("process.list", plan!.ToolId);
    }

    [Fact]
    public void Plan_routes_system_keyword()
    {
        var plan = CommandPlanner.Plan("system info", Tools());
        Assert.NotNull(plan);
        Assert.Contains("system.get_info", plan!.ToolId);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("讲个笑话")]
    public void Plan_returns_null_when_no_tool_matches(string prompt)
    {
        Assert.Null(CommandPlanner.Plan(prompt, Tools()));
    }

    [Fact]
    public void Plan_returns_null_with_empty_tool_list()
    {
        Assert.Null(CommandPlanner.Plan("打开 notepad", Array.Empty<ToolSummary>()));
    }

    [Theory]
    [InlineData("打开notepad。", "notepad")]
    [InlineData("运行 \"spotify\" 的", "spotify")]
    [InlineData("launch vlc", "vlc")]
    public void ExtractTarget_strips_trailing_punctuation(string text, string expected)
    {
        Assert.Equal(expected, CommandPlanner.ExtractTarget(text));
    }

    [Fact]
    public void ExtractTarget_returns_empty_for_no_keyword()
    {
        Assert.Equal(string.Empty, CommandPlanner.ExtractTarget("今天天气不错"));
    }
}
