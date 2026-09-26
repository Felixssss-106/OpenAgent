namespace OpenAgent.Windows.UI.Services;

/// <summary>
/// Pure arithmetic for the M-17 long-press confirm (<c>design/motion.md</c>).
/// Extracted from <c>CommandCenterWindow</c> so the timing is unit-testable
/// without a <c>DispatcherQueue</c>. The timer ticks every 50ms; 24 ticks
/// (1200ms = <see cref="Motion.LongPress"/>) is the confirm threshold.
/// </summary>
public static class LongPressCounter
{
    /// <summary>Ticks happen every 50ms on the long-press timer.</summary>
    public const int TickIntervalMs = 50;

    /// <summary>
    /// Elapsed milliseconds for the given tick count, capped at
    /// <see cref="Motion.LongPress"/> so a stuck timer never over-fills the bar.
    /// </summary>
    public static int ProgressMs(int ticks) =>
        Math.Min(ticks * TickIntervalMs, 1200);

    /// <summary>True once the long-press threshold (1200ms) has been reached.</summary>
    public static bool IsComplete(int ticks) =>
        ticks * TickIntervalMs >= 1200;

    /// <summary>Progress as a 0..1 ratio for a <c>ProgressBar.Value</c> (Maximum = 1200).</summary>
    public static double ProgressRatio(int ticks) =>
        ProgressMs(ticks) / 1200.0;
}
