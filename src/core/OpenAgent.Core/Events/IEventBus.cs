namespace OpenAgent.Core.Events;

/// <summary>
/// The one way infrastructure talks to the UI (spec section 164). UI subscribes,
/// never polls.
/// </summary>
public interface IEventBus
{
    /// <summary>Register a handler. Returns a disposable that unsubscribes.</summary>
    IDisposable Subscribe<TEvent>(Func<TEvent, CancellationToken, Task> handler)
        where TEvent : notnull;

    Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull;
}
