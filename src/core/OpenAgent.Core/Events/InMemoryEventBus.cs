using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace OpenAgent.Core.Events;

/// <summary>
/// In-process bus. One slow or throwing handler must not stop the others from
/// seeing the event, so failures are logged and collected.
/// </summary>
public sealed class InMemoryEventBus : IEventBus
{
    private readonly ILogger<InMemoryEventBus> _logger;
    private readonly Dictionary<Type, List<Func<object, CancellationToken, Task>>> _handlers = new();
    private readonly Lock _gate = new();

    public InMemoryEventBus()
        : this(NullLogger<InMemoryEventBus>.Instance)
    {
    }

    public InMemoryEventBus(ILogger<InMemoryEventBus> logger)
    {
        _logger = logger;
    }

    public IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);

        Func<object, CancellationToken, Task> boxed = (e, ct) => handler((TEvent)e, ct);

        lock (_gate)
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list))
            {
                list = new List<Func<object, CancellationToken, Task>>();
                _handlers[typeof(TEvent)] = list;
            }

            list.Add(boxed);
        }

        return new Subscription(this, typeof(TEvent), boxed);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        List<Func<object, CancellationToken, Task>> snapshot;
        lock (_gate)
        {
            snapshot = _handlers.TryGetValue(typeof(TEvent), out var list)
                ? new List<Func<object, CancellationToken, Task>>(list)
                : new List<Func<object, CancellationToken, Task>>();
        }

        foreach (var handler in snapshot)
        {
            try
            {
                await handler(@event, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Event handler for {EventType} failed.", typeof(TEvent).Name);
            }
        }
    }

    private void Unsubscribe(Type eventType, Func<object, CancellationToken, Task> handler)
    {
        lock (_gate)
        {
            if (_handlers.TryGetValue(eventType, out var list))
            {
                list.Remove(handler);
            }
        }
    }

    private sealed class Subscription : IDisposable
    {
        private readonly InMemoryEventBus _bus;
        private readonly Type _eventType;
        private readonly Func<object, CancellationToken, Task> _handler;

        public Subscription(InMemoryEventBus bus, Type eventType, Func<object, CancellationToken, Task> handler)
        {
            _bus = bus;
            _eventType = eventType;
            _handler = handler;
        }

        public void Dispose() => _bus.Unsubscribe(_eventType, _handler);
    }
}
