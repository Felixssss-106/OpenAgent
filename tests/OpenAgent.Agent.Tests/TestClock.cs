using OpenAgent.Core.Time;

namespace OpenAgent.Agent.Tests;

/// <summary>Manual clock so approval expiry is tested without sleeping.</summary>
public sealed class TestClock : IUtcClock
{
    public TestClock(DateTimeOffset? start = null) => UtcNow = start ?? DateTimeOffset.UtcNow;

    public DateTimeOffset UtcNow { get; set; }

    public void Advance(TimeSpan by) => UtcNow = UtcNow.Add(by);
}
