using Microsoft.Extensions.DependencyInjection;
using OpenAgent.Agent;
using OpenAgent.Core.Error;
using OpenAgent.Core.Events;
using OpenAgent.Core.Tasks;
using OpenAgent.Core.Time;

namespace OpenAgent.Agent.Tests;

public sealed class AgentTaskServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "openagent-tests", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _provider;

    public AgentTaskServiceTests()
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
            // Locked WAL file; the temp directory is disposable anyway.
        }
    }

    [Fact]
    public void The_composition_root_resolves_the_whole_stack()
    {
        Assert.NotNull(_provider.GetRequiredService<IEventBus>());
        Assert.NotNull(_provider.GetRequiredService<AgentTaskService>());
        Assert.NotNull(_provider.GetRequiredService<ToolExecutor>());
        Assert.NotNull(_provider.GetRequiredService<ApprovalService>());
        Assert.Equal(3, _provider.GetRequiredService<OpenAgent.Tools.ToolRegistry>().All().Count);
    }

    [Fact]
    public async Task Creating_a_task_starts_it_queued_and_announces_it()
    {
        var service = _provider.GetRequiredService<AgentTaskService>();
        var bus = _provider.GetRequiredService<IEventBus>();
        TaskStarted? announced = null;
        bus.Subscribe<TaskStarted>((e, ct) =>
        {
            announced = e;
            return Task.CompletedTask;
        });

        var task = await service.CreateAsync("整理下载目录", "openagent.native");

        Assert.Equal(AgentTaskStatus.Queued, task.Status);
        Assert.NotNull(announced);
        Assert.Equal(task.Id, announced!.TaskId);
    }

    [Fact]
    public async Task Illegal_transitions_are_refused()
    {
        var service = _provider.GetRequiredService<AgentTaskService>();
        var task = await service.CreateAsync("x", "openagent.native");

        Assert.Throws<OpenAgentException>(() =>
            service.TransitionAsync(task.Id, AgentTaskStatus.Completed).GetAwaiter().GetResult());
    }

    [Fact]
    public async Task A_task_can_run_to_completion()
    {
        var service = _provider.GetRequiredService<AgentTaskService>();
        var task = await service.CreateAsync("看看系统状态", "openagent.native");

        await service.TransitionAsync(task.Id, AgentTaskStatus.Planning);
        await service.TransitionAsync(task.Id, AgentTaskStatus.Running);
        await service.TransitionAsync(task.Id, AgentTaskStatus.Completed);

        var loaded = service.Get(task.Id)!;
        Assert.Equal(AgentTaskStatus.Completed, loaded.Status);
        Assert.NotNull(loaded.CompletedAtUtc);
    }

    [Fact]
    public async Task Cancelling_a_task_cancels_its_token_not_the_process()
    {
        var service = _provider.GetRequiredService<AgentTaskService>();
        var task = await service.CreateAsync("长任务", "openagent.native");
        await service.TransitionAsync(task.Id, AgentTaskStatus.Running);

        await service.CancelAsync(task.Id);

        Assert.True(service.TokenFor(task.Id).IsCancellationRequested);
        Assert.Equal(AgentTaskStatus.Cancelled, service.Get(task.Id)!.Status);
    }

    [Fact]
    public async Task Events_are_appended_in_order()
    {
        var service = _provider.GetRequiredService<AgentTaskService>();
        var task = await service.CreateAsync("x", "openagent.native");

        await service.AppendEventAsync(task.Id, OpenAgent.Core.Domain.TaskEventKinds.Assistant, "我准备检查项目结构");
        await service.AppendEventAsync(task.Id, OpenAgent.Core.Domain.TaskEventKinds.Tool, "directory.list");

        var events = await service.EventsAsync(task.Id);

        Assert.Equal(2, events.Count);
        Assert.Equal("assistant", events[0].Kind);
        Assert.Equal("tool", events[1].Kind);
    }

    [Fact]
    public async Task An_unknown_task_cannot_be_transitioned()
    {
        var service = _provider.GetRequiredService<AgentTaskService>();

        await Assert.ThrowsAsync<OpenAgentException>(
            () => service.TransitionAsync("missing", AgentTaskStatus.Running));
    }
}
