using OpenAgent.Windows.UI.Services;
using Xunit;

namespace OpenAgent.Windows.UI.Tests;

/// <summary>M-17 long-press confirm timing arithmetic (spec motion.md).</summary>
public sealed class LongPressCounterTests
{
    [Fact]
    public void ProgressMs_at_zero_ticks_is_zero() =>
        Assert.Equal(0, LongPressCounter.ProgressMs(0));

    [Fact]
    public void ProgressMs_at_one_tick_is_50() =>
        Assert.Equal(50, LongPressCounter.ProgressMs(1));

    [Fact]
    public void ProgressMs_at_23_ticks_is_1150() =>
        Assert.Equal(1150, LongPressCounter.ProgressMs(23));

    [Fact]
    public void ProgressMs_at_24_ticks_is_1200() =>
        Assert.Equal(1200, LongPressCounter.ProgressMs(24));

    [Fact]
    public void ProgressMs_at_30_ticks_is_capped_at_1200() =>
        Assert.Equal(1200, LongPressCounter.ProgressMs(30));

    [Fact]
    public void IsComplete_at_zero_is_false() =>
        Assert.False(LongPressCounter.IsComplete(0));

    [Fact]
    public void IsComplete_at_23_ticks_is_false() =>
        Assert.False(LongPressCounter.IsComplete(23));

    [Fact]
    public void IsComplete_at_24_ticks_is_true() =>
        Assert.True(LongPressCounter.IsComplete(24));

    [Fact]
    public void IsComplete_at_30_ticks_is_true() =>
        Assert.True(LongPressCounter.IsComplete(30));

    [Fact]
    public void ProgressRatio_at_zero_is_zero() =>
        Assert.Equal(0.0, LongPressCounter.ProgressRatio(0));

    [Fact]
    public void ProgressRatio_at_24_ticks_is_one() =>
        Assert.Equal(1.0, LongPressCounter.ProgressRatio(24));

    [Fact]
    public void ProgressRatio_at_12_ticks_is_half() =>
        Assert.Equal(0.5, LongPressCounter.ProgressRatio(12));
}
