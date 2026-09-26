using global::Windows.UI.ViewManagement;

namespace OpenAgent.Windows;

/// <summary>
/// Motion design tokens (spec file <c>design/motion.md</c> section 1).
/// Durations are a stepped ladder so no animation picks an arbitrary
/// millisecond count, and <see cref="Enabled"/> is the WinUI equivalent of
/// <c>prefers-reduced-motion</c>: when the user has disabled animations in
/// Windows we set the terminal values and skip the storyboard instead of
/// forcing motion on someone who asked for none.
/// </summary>
public static class Motion
{
    /// <summary>Instant feedback inside a state change (a tap, a focus ring).</summary>
    public const int Instant = 120;

    /// <summary>A state change itself (toggle, selection).</summary>
    public const int State = 200;

    /// <summary>Element leaving the composition.</summary>
    public const int Exit = 240;

    /// <summary>A layout shift (panel rearrange, navigation swap).</summary>
    public const int Layout = 320;

    /// <summary>An element entering the composition.</summary>
    public const int Enter = 480;

    /// <summary>Long-press confirm for high/critical approvals (M-17).</summary>
    public const int LongPress = 1200;

    /// <summary>
    /// <c>false</c> when the user turned Windows animations off — honour the
    /// preference and skip every storyboard in the shell.
    /// </summary>
    public static bool Enabled => new UISettings().AnimationsEnabled;
}
