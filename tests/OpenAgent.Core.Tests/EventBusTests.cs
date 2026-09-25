using OpenAgent.Core.Events;

namespace OpenAgent.Core.Tests;

public sealed class EventBusTests
{
    [Fact]
    public async Task Publish_reaches_every_subscriber()
    {
        var bus = new InMemoryEventBus();
        var first = 0;
        var second = 0;

        bus.Subscribe<TaskStarted>((e, ct) =>
        {
            Interlocked.Increment(ref first);
            return Task.CompletedTask;
        });
        bus.Subscribe<TaskStarted>((e, ct) =>
        {
            Interlocked.Increment(ref second);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new TaskStarted("t1", "do it", "openagent.native", DateTimeOffset.UtcNow));

        Assert.Equal(1, first);
        Assert.Equal(1, second);
    }

    [Fact]
    public async Task Unsubscribing_stops_delivery()
    {
        var bus = new InMemoryEventBus();
        var count = 0;
        var subscription = bus.Subscribe<TaskStarted>((e, ct) =>
        {
            Interlocked.Increment(ref count);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new TaskStarted("t1", "x", "p", DateTimeOffset.UtcNow));
        subscription.Dispose();
        await bus.PublishAsync(new TaskStarted("t2", "x", "p", DateTimeOffset.UtcNow));

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task A_throwing_handler_does_not_hide_the_event_from_others()
    {
        var bus = new InMemoryEventBus();
        var reached = false;

        bus.Subscribe<TaskStarted>((e, ct) => throw new InvalidOperationException("boom"));
        bus.Subscribe<TaskStarted>((e, ct) =>
        {
            reached = true;
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new TaskStarted("t1", "x", "p", DateTimeOffset.UtcNow));

        Assert.True(reached);
    }

    [Fact]
    public async Task Events_are_delivered_only_to_their_own_type()
    {
        var bus = new InMemoryEventBus();
        var wrong = 0;

        bus.Subscribe<TaskStarted>((e, ct) =>
        {
            Interlocked.Increment(ref wrong);
            return Task.CompletedTask;
        });

        await bus.PublishAsync(new ApprovalResolved("a1", "t1", true, "user"));

        Assert.Equal(0, wrong);
    }
}
