using System.Globalization;
using System.Text.RegularExpressions;

namespace OpenAgent.Security;

/// <summary>
/// Scrubs secrets out of anything that will be logged, stored in an audit entry,
/// shown in an approval card, or exported in a diagnostics bundle
/// (spec sections 100, 211, 245).
/// </summary>
public static partial class SecretRedactor
{
    private static readonly string[] SensitiveKeyTokens =
    {
        "apikey", "api_key", "token", "secret", "password", "passwd", "pwd",
        "credential", "authorization", "privatekey", "private_key",
    };

    private const string Mask = "***";

    /// <summary>Redact <c>KEY=VALUE</c> / <c>"KEY": "VALUE"</c> pairs whose key looks sensitive.</summary>
    public static string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var withPairs = KeyValuePattern().Replace(text, m =>
            ContainsSensitiveToken(m.Groups["key"].Value)
                ? m.Value.Replace(m.Groups["value"].Value, Mask, StringComparison.Ordinal)
                : m.Value);

        return SkPattern().Replace(withPairs, Mask);
    }

    public static bool ContainsSensitiveToken(string key)
    {
        var normalized = key.Replace("-", string.Empty, StringComparison.Ordinal)
            .Replace("_", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal)
            .Replace("\"", string.Empty, StringComparison.Ordinal)
            .ToLowerInvariant();

        foreach (var token in SensitiveKeyTokens)
        {
            if (normalized.Contains(token, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Collapses a value to a fixed length so log sizes cannot leak content length.</summary>
    public static string Summary(string? text, int maxLength = 240)
    {
        var redacted = Redact(text);
        if (redacted.Length <= maxLength)
        {
            return redacted;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{redacted[..maxLength]}…");
    }

    [GeneratedRegex(@"(?<key>[\p{L}\p{N}_\-\.""']{2,64})\s*[:=]\s*""?(?<value>[^"",;}\r\n]{1,})""?")]
    private static partial Regex KeyValuePattern();

    [GeneratedRegex(@"\bsk-[A-Za-z0-9_\-]{8,}\b")]
    private static partial Regex SkPattern();
}
