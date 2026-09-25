namespace OpenAgent.Core.Error;

/// <summary>
/// Stable error code families (spec section 235). Codes are never localised;
/// the human-facing message is.
/// </summary>
public static class ErrorCodes
{
    public const int Network = 1000;
    public const int Auth = 2000;
    public const int Permission = 3000;
    public const int Agent = 4000;
    public const int Tool = 5000;
    public const int File = 6000;
    public const int Provider = 7000;
    public const int Plugin = 8000;

    public static string Format(int family, int index) => $"OA-{family + index}";

    public static string NetworkError(int index) => Format(Network, index);

    public static string AuthError(int index) => Format(Auth, index);

    public static string PermissionError(int index) => Format(Permission, index);

    public static string AgentError(int index) => Format(Agent, index);

    public static string ToolError(int index) => Format(Tool, index);

    public static string FileError(int index) => Format(File, index);

    public static string ProviderError(int index) => Format(Provider, index);

    public static string PluginError(int index) => Format(Plugin, index);

    /// <summary>Which family a code belongs to, or null when it is malformed.</summary>
    public static int? FamilyOf(string? code)
    {
        if (string.IsNullOrEmpty(code) || !code.StartsWith("OA-", StringComparison.Ordinal))
        {
            return null;
        }

        if (!int.TryParse(code.AsSpan(3), out var raw))
        {
            return null;
        }

        var family = raw - (raw % 1000);
        return family is >= 1000 and <= 8000 ? family : null;
    }
}
