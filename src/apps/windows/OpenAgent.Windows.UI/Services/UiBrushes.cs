using System.Collections.Generic;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace OpenAgent.Windows.UI.Services;

/// <summary>
/// Theme-aware brush lookup that never throws. Tokens live inside
/// <c>ThemeDictionaries</c>, so a resource can be missing at runtime (an older
/// token file, a dictionary that has not merged yet); every caller passes the
/// fallback it wants instead of crashing the page (spec section 105).
/// </summary>
public static class UiBrushes
{
    /// <summary>
    /// Last-resort colours for when a token is genuinely absent from the merged
    /// dictionaries. They mirror the light Pixso palette so a miss degrades to
    /// something on-palette instead of an arbitrary grey, and they live here
    /// because the same handful was previously repeated at every call site.
    /// </summary>
    public static class Fallback
    {
        public const string Accent = "#FF007AFF";
        public const string Error = "#FFFF3B30";
        public const string StatusNeutral = "#FF8E8E93";
        public const string StatusText = "#FF6E6E73";
        public const string ChipBackground = "#FFE5E5EA";
        public const string ChipForeground = "#FF6E6E73";
        public const string Quaternary = "#FFAEAEB2";
        public const string Online = "#FF34C759";
        public const string Transparent = "#00000000";
    }

    /// <summary>
    /// The element code-behind lookups resolve their palette against — the shell
    /// sets it to its own root. Null keeps the plain application lookup, which
    /// follows the system theme; WinUI 3 offers no safe way to change the
    /// application theme at runtime (it fail-fast crashes the XAML engine), so an
    /// overridden shell needs its own route to the right palette.
    /// </summary>
    public static FrameworkElement? Context { get; set; }

    public static Brush Get(string key, string fallbackHex)
    {
        if (Application.Current?.Resources is { } app &&
            TryGet(app, key, Context?.ActualTheme, out var themed))
        {
            return themed;
        }

        return new SolidColorBrush(Parse(fallbackHex));
    }

    /// <summary>
    /// With a themed element to ask for, read that palette's theme dictionary
    /// first: a plain lookup on the merged set answers with the palette the
    /// application object carries, which is the system theme, not this window's.
    /// </summary>
    private static bool TryGet(
        ResourceDictionary root, string key, ElementTheme? theme, out Brush brush)
    {
        if (theme is { } asked)
        {
            foreach (var dictionary in Walk(root))
            {
                if (TryInThemeDictionaries(dictionary, key, asked, out brush))
                {
                    return true;
                }
            }
        }

        if (root.TryGetValue(key, out var value) && value is Brush own)
        {
            brush = own;
            return true;
        }

        brush = null!;
        return false;
    }

    private static IEnumerable<ResourceDictionary> Walk(ResourceDictionary root)
    {
        var queue = new Queue<ResourceDictionary>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var dictionary = queue.Dequeue();
            yield return dictionary;

            foreach (var merged in dictionary.MergedDictionaries)
            {
                queue.Enqueue(merged);
            }
        }
    }

    /// <summary>
    /// The brush instances inside a theme dictionary are dehydrated while that
    /// theme is inactive, and their {StaticResource} colour then resolves against
    /// whichever palette is live — asking the Light dictionary for AccentBrush on
    /// a dark system hands back the dark blue. The generator writes a literal
    /// {Name}Color beside every {Name}Brush, and a literal survives the swap, so
    /// the colour is read and the brush built here.
    /// </summary>
    private static bool TryInThemeDictionaries(
        ResourceDictionary dictionary, string key, ElementTheme theme, out Brush brush)
    {
        brush = null!;

        foreach (var name in ThemeNames(theme))
        {
            if (dictionary.ThemeDictionaries.TryGetValue(name, out var entry) is not true ||
                entry is not ResourceDictionary themed)
            {
                continue;
            }

            if (themed.TryGetValue(ColorKeyFor(key), out var colour) &&
                colour is global::Windows.UI.Color literal)
            {
                brush = new SolidColorBrush(literal);
                return true;
            }

            if (themed.TryGetValue(key, out var value) && value is Brush found)
            {
                brush = found;
                return true;
            }
        }

        return false;
    }

    /// <summary>TabHighlightBrush → TabHighlightColor; anything else asks for a
    /// key that cannot exist and falls through to the brush itself.</summary>
    private static string ColorKeyFor(string key) =>
        key.EndsWith("Brush", StringComparison.Ordinal)
            ? string.Concat(key.AsSpan(0, key.Length - "Brush".Length), "Color")
            : key + ".Color";

    /// <summary>An element's ActualTheme is already resolved, so only its own name
    /// and the legacy Default section can match.</summary>
    private static IEnumerable<string> ThemeNames(ElementTheme theme) =>
        theme == ElementTheme.Dark ? new[] { "Dark", "Default" } : new[] { "Light", "Default" };

    public static Brush Status(string key) => Get(key, Fallback.StatusNeutral);

    /// <summary>Brush for a fallback hex, for view-model defaults that are built
    /// before a token key is known.</summary>
    public static Brush FromHex(string hex) => new SolidColorBrush(Parse(hex));

    private static global::Windows.UI.Color Parse(string hex)
    {
        var span = hex.AsSpan();
        if (span.Length > 0 && span[0] == '#')
        {
            span = span[1..];
        }

        var alpha = (byte)0xFF;
        if (span.Length == 8)
        {
            alpha = Byte(span[..2]);
            span = span[2..];
        }

        if (span.Length != 6)
        {
            return Microsoft.UI.Colors.Gray;
        }

        return global::Windows.UI.Color.FromArgb(
            alpha, Byte(span[..2]), Byte(span[2..4]), Byte(span[4..6]));
    }

    private static byte Byte(ReadOnlySpan<char> text) =>
        byte.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
}
