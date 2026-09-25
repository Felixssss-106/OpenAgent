namespace OpenAgent.Core.Time;

/// <summary>
/// Injectable clock so approval expiry and retry tests do not sleep.
/// </summary>
public interface IUtcClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemUtcClock : IUtcClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
