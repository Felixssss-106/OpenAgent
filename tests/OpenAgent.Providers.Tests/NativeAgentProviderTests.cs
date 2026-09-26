using OpenAgent.Core;
using OpenAgent.Providers;
using Xunit;

namespace OpenAgent.Providers.Tests;

public sealed class NativeAgentProviderTests
{
    private static readonly ProviderToolInfo[] Tools =
    {
        new("system.get_info", "System Info", "Read system info", RiskLevel.Safe, true),
        new("process.list", "Process List", "List running processes", RiskLevel.Safe, true),
        new("app.launch", "Launch App", "Launch an application", RiskLevel.Low, false),
        new("file.delete", "Delete File", "Delete a file", RiskLevel.High, false),
    };

    private static readonly NativeAgentProvider Provider = new();

    [Fact]
    public void Id_and_display_name_are_stable()
    {
        Assert.Equal("openagent.native", Provider.Id);
        Assert.Equal("OpenAgent Native", Provider.DisplayName);
    }

    [Fact]
    public void Capabilities_exclude_streaming_image_terminal_and_mcp()
    {
        var caps = Provider.Capabilities;
        Assert.True(caps.HasFlag(AgentCapabilities.ToolCalling));
        Assert.True(caps.HasFlag(AgentCapabilities.Approval));
        Assert.False(caps.HasFlag(AgentCapabilities.Streaming));
        Assert.False(caps.HasFlag(AgentCapabilities.ImageInput));
        Assert.False(caps.HasFlag(AgentCapabilities.InteractiveTerminal));
        Assert.False(caps.HasFlag(AgentCapabilities.Mcp));
    }

    [Theory]
    [InlineData("打开 notepad", "app.launch", "notepad")]
    [InlineData("启动 calc", "app.launch", "calc")]
    [InlineData("launch spotify", "app.launch", "spotify")]
    [InlineData("查看进程", "process.list", "")]
    [InlineData("system info", "system.get_info", "")]
    [InlineData("内存占用", "system.get_info", "")]
    public void Plan_routes_known_keywords_to_the_right_tool(string prompt, string expectedTool, string expectedTarget)
    {
        var plan = NativeAgentProvider.Plan(prompt, Tools);
        Assert.NotNull(plan);
        Assert.Equal(expectedTool, plan!.ToolId);

        if (expectedTarget.Length > 0)
        {
            Assert.Contains(expectedTarget, plan.ArgumentsJson);
        }
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("讲个笑话")]
    public void Plan_returns_null_when_no_tool_matches(string prompt)
    {
        Assert.Null(NativeAgentProvider.Plan(prompt, Tools));
    }

    [Fact]
    public void Plan_returns_null_with_empty_tool_set()
    {
        Assert.Null(NativeAgentProvider.Plan("打开 notepad", Array.Empty<ProviderToolInfo>()));
    }

    [Theory]
    [InlineData("打开notepad。", "notepad")]
    [InlineData("运行 \"spotify\" 的", "spotify")]
    [InlineData("launch vlc", "vlc")]
    public void ExtractTarget_strips_trailing_punctuation(string text, string expected)
    {
        Assert.Equal(expected, NativeAgentProvider.ExtractTarget(text));
    }

    [Fact]
    public async Task SendPrompt_emits_a_thought_then_a_tool_call()
    {
        var session = await Provider.CreateSessionAsync("task-1");
        var steps = await Provider.SendPromptAsync(session, "查看进程", Tools);

        Assert.Equal(2, steps.Count);
        Assert.Equal(ProviderStepKind.Thought, steps[0].Kind);
        Assert.Equal(ProviderStepKind.ToolCall, steps[1].Kind);
        Assert.Equal("process.list", steps[1].ToolId);
    }

    [Fact]
    public async Task SendPrompt_emits_an_error_step_when_nothing_matches()
    {
        var session = await Provider.CreateSessionAsync("task-2");
        var steps = await Provider.SendPromptAsync(session, "讲个笑话", Tools);

        Assert.Single(steps);
        Assert.Equal(ProviderStepKind.Error, steps[0].Kind);
    }

    [Fact]
    public async Task HealthCheck_is_always_healthy()
    {
        Assert.Equal(AgentHealth.Healthy, await Provider.HealthCheckAsync());
    }

    [Fact]
    public async Task Stop_and_resume_are_no_ops_that_complete()
    {
        var session = await Provider.CreateSessionAsync("task-3");
        await Provider.StopAsync(session);
        await Provider.ResumeAsync(session);
    }
}
