using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAgent.Core.Domain;
using OpenAgent.Core.Events;
using OpenAgent.Core.Error;
using OpenAgent.Core.Tasks;
using OpenAgent.Core.Time;
using OpenAgent.Storage.Repositories;

namespace OpenAgent.Agent;

/// <summary>
/// Task lifecycle and the cancellation contract: stopping a task cancels its
/// work, it does not kill the application (spec sections 35, 97, 203).
/// </summary>
public sealed class AgentTaskService
{
    private readonly TaskRepository _tasks;
    private readonly TaskEventRepository _events;
    private readonly IEventBus _bus;
    private readonly IUtcClock _clock;
    private readonly ILogger<AgentTaskService> _logger;
    private readonly Dictionary<string, CancellationTokenSource> _cancellation = new(StringComparer.Ordinal);
    private readonly Lock _gate = new();

    public AgentTaskService(
        TaskRepository tasks,
        TaskEventRepository events,
        IEventBus bus,
        IUtcClock clock,
        ILogger<AgentTaskService>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(tasks);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(bus);
        ArgumentNullException.ThrowIfNull(clock);

        _tasks = tasks;
        _events = events;
        _bus = bus;
        _clock = clock;
        _logger = logger ?? NullLogger<AgentTaskService>.Instance;
    }

    public async Task<AgentTask> CreateAsync(
        string prompt,
        string providerId,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            throw new ArgumentException("A task needs a prompt.", nameof(prompt));
        }

        var now = _clock.UtcNow;
        var task = new AgentTask
        {
            Prompt = prompt,
            ProviderId = providerId,
            Status = AgentTaskStatus.Queued,
            WorkingDirectory = workingDirectory,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        _tasks.Insert(task);

        lock (_gate)
        {
            _cancellation[task.Id] = new CancellationTokenSource();
        }

        await _bus.PublishAsync(
            new TaskStarted(task.Id, task.Prompt, task.ProviderId, now),
            cancellationToken).ConfigureAwait(false);

        return task;
    }

    public AgentTask? Get(string taskId) => _tasks.Get(taskId);

    public async Task<AgentTask> TransitionAsync(
        string taskId,
        AgentTaskStatus to,
        string? error = null,
        CancellationToken cancellationToken = default)
    {
        var task = _tasks.Get(taskId)
                   ?? throw new OpenAgentException(ErrorCodes.AgentError(1), $"任务不存在：{taskId}");

        var from = task.Status;
        if (!TaskStateMachine.CanTransition(from, to))
        {
            throw new OpenAgentException(
                ErrorCodes.AgentError(2),
                $"不允许的任务状态转换：{from} → {to}");
        }

        var now = _clock.UtcNow;
        task.Status = to;
        task.UpdatedAtUtc = now;
        task.Error = error;
        if (AgentTaskStatuses.IsTerminal(to))
        {
            task.CompletedAtUtc = now;
        }

        _tasks.Update(task);

        await _bus.PublishAsync(
            new TaskStatusChanged(task.Id, from, to),
            cancellationToken).ConfigureAwait(false);

        return task;
    }

    public Task AppendEventAsync(
        string taskId,
        string kind,
        string text,
        string? dataJson = null,
        CancellationToken cancellationToken = default)
    {
        _events.Insert(new TaskEventRecord
        {
            TaskId = taskId,
            Kind = kind,
            Text = text,
            DataJson = dataJson,
            TimestampUtc = _clock.UtcNow,
        });

        return cancellationToken.IsCancellationRequested
            ? Task.FromCanceled(cancellationToken)
            : Task.CompletedTask;
    }

    public Task<IReadOnlyList<AgentTask>> RecentAsync(int limit = 50) =>
        Task.FromResult(_tasks.Recent(limit));

    public Task<IReadOnlyList<TaskEventRecord>> EventsAsync(string taskId) =>
        Task.FromResult(_events.ForTask(taskId));

    /// <summary>Stopping is cancellation, never process termination.</summary>
    public async Task CancelAsync(string taskId, CancellationToken cancellationToken = default)
    {
        CancellationTokenSource? cts;
        lock (_gate)
        {
            _cancellation.TryGetValue(taskId, out cts);
        }

        if (cts is not null)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }

        var task = _tasks.Get(taskId);
        if (task is not null && !AgentTaskStatuses.IsTerminal(task.Status))
        {
            await TransitionAsync(taskId, AgentTaskStatus.Cancelled, cancellationToken: cancellationToken)
                .ConfigureAwait(false);
        }

        _logger.LogInformation("Task {TaskId} cancelled.", taskId);
    }

    public CancellationToken TokenFor(string taskId)
    {
        lock (_gate)
        {
            return _cancellation.TryGetValue(taskId, out var cts) ? cts.Token : CancellationToken.None;
        }
    }

    public void Release(string taskId)
    {
        lock (_gate)
        {
            if (_cancellation.Remove(taskId, out var cts))
            {
                cts.Dispose();
            }
        }
    }
}
