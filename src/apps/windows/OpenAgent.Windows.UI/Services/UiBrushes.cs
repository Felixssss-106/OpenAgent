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
    }

    public static Brush Get(string key, string fallbackHex)
    {
        var resources = Application.Current?.Resources;
        if (resources is not null &&
            resources.TryGetValue(key, out var value) &&
            value is Brush brush)
        {
            return brush;
        }

        return new SolidColorBrush(Parse(fallbackHex));
    }

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
