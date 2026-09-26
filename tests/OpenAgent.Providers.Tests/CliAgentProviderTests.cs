using System;
using System.Threading;
using System.Threading.Tasks;
using OpenAgent.Core;
using Xunit;

namespace OpenAgent.Providers.Tests;

public sealed class CliAgentProviderTests
{
    private static readonly CliInvocationProfile Profile = new(
        "openagent.cli.test", "Test CLI", new[] { "testcli" },
        AgentCapabilities.ToolCalling | AgentCapabilities.Approval,
        new[] { "-p" });

    private sealed class FakeRunner : IProcessRunner
    {
        private readonly ProcessRunResult? _result;
        private readonly Exception? _throw;
        public string? LastFileName;
        public string? LastArguments;

        public FakeRunner(ProcessRunResult result) => _result = result;
        public FakeRunner(Exception ex) => _throw = ex;

        public Task<ProcessRunResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        {
            LastFileName = fileName;
            LastArguments = arguments;
            return _throw is not null
                ? Task.FromException<ProcessRunResult>(_throw)
                : Task.FromResult(_result!);
        }
    }

    private static CliAgentProvider Provider(FakeRunner runner) => new(Profile, runner);

    [Fact]
    public async Task SendPrompt_emits_thought_toolcall_and_result()
    {
        var runner = new FakeRunner(new ProcessRunResult(0, "done", string.Empty));
        var provider = Provider(runner);
        var session = await provider.CreateSessionAsync("task-1");

        var steps = await provider.SendPromptAsync(session, "打开 notepad", Array.Empty<ProviderToolInfo>());

        Assert.Equal(3, steps.Count);
        Assert.Equal(ProviderStepKind.Thought, steps[0].Kind);
        Assert.Equal(ProviderStepKind.ToolCall, steps[1].Kind);
        Assert.Equal(ProviderStepKind.ToolResult, steps[2].Kind);
        Assert.Equal("done", steps[2].Text);
    }

    [Fact]
    public async Task SendPrompt_passes_prompt_as_final_quoted_argument()
    {
        var runner = new FakeRunner(new ProcessRunResult(0, "ok", string.Empty));
        var provider = Provider(runner);
        var session = await provider.CreateSessionAsync("task-2");

        await provider.SendPromptAsync(session, "打开 notepad", Array.Empty<ProviderToolInfo>());

        Assert.Equal("testcli", runner.LastFileName);
        Assert.Equal("-p \"打开 notepad\"", runner.LastArguments);
    }

    [Fact]
    public async Task SendPrompt_surfaces_stderr_when_exit_code_nonzero()
    {
        var runner = new FakeRunner(new ProcessRunResult(1, string.Empty, "boom"));
        var provider = Provider(runner);
        var session = await provider.CreateSessionAsync("task-3");

        var steps = await provider.SendPromptAsync(session, "x", Array.Empty<ProviderToolInfo>());

        var last = steps[^1];
        Assert.Equal(ProviderStepKind.Error, last.Kind);
        Assert.Contains("boom", last.Text);
    }

    [Fact]
    public async Task SendPrompt_surfaces_exception_as_error_step()
    {
        var runner = new FakeRunner(new InvalidOperationException("nope"));
        var provider = Provider(runner);
        var session = await provider.CreateSessionAsync("task-4");

        var steps = await provider.SendPromptAsync(session, "x", Array.Empty<ProviderToolInfo>());

        var last = steps[^1];
        Assert.Equal(ProviderStepKind.Error, last.Kind);
        Assert.Contains("nope", last.Text);
    }

    [Fact]
    public void Capabilities_come_from_profile()
    {
        var provider = Provider(new FakeRunner(new ProcessRunResult(0, "", "")));
        Assert.True(provider.Capabilities.HasFlag(AgentCapabilities.ToolCalling));
        Assert.True(provider.Capabilities.HasFlag(AgentCapabilities.Approval));
    }
}
