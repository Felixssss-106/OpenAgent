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

    public static Brush Status(string key) => Get(key, "#FF8A8F98");

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
