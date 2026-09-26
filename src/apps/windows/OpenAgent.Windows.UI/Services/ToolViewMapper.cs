using OpenAgent.Core;

namespace OpenAgent.Windows.UI.Services;

/// <summary>
/// Pure mapping from risk levels and tool ids to view strings. Colour is never
/// the only carrier of meaning, so every risk also gets a word (spec section 105).
/// </summary>
public static class ToolViewMapper
{
    public const string RiskSafeBrushKey = "RiskSafeBrush";
    public const string RiskLowBrushKey = "RiskLowBrush";
    public const string RiskMediumBrushKey = "RiskMediumBrush";
    public const string RiskHighBrushKey = "RiskHighBrush";
    public const string RiskCriticalBrushKey = "RiskCriticalBrush";

    public const string RiskSafeBackgroundKey = "RiskSafeBgBrush";
    public const string RiskLowBackgroundKey = "RiskLowBgBrush";
    public const string RiskMediumBackgroundKey = "RiskMediumBgBrush";
    public const string RiskHighBackgroundKey = "RiskHighBgBrush";
    public const string RiskCriticalBackgroundKey = "RiskCriticalBgBrush";

    public static string RiskLabel(RiskLevel risk) => risk switch
    {
        RiskLevel.Safe => "只读",
        RiskLevel.Low => "低风险",
        RiskLevel.Medium => "中风险",
        RiskLevel.High => "高风险",
        RiskLevel.Critical => "极高风险",
        _ => "未知",
    };

    public static string RiskBrushKey(RiskLevel risk) => risk switch
    {
        RiskLevel.Safe => RiskSafeBrushKey,
        RiskLevel.Low => RiskLowBrushKey,
        RiskLevel.Medium => RiskMediumBrushKey,
        RiskLevel.High => RiskHighBrushKey,
        RiskLevel.Critical => RiskCriticalBrushKey,
        _ => RiskMediumBrushKey,
    };

    public static string RiskBackgroundKey(RiskLevel risk) => risk switch
    {
        RiskLevel.Safe => RiskSafeBackgroundKey,
        RiskLevel.Low => RiskLowBackgroundKey,
        RiskLevel.Medium => RiskMediumBackgroundKey,
        RiskLevel.High => RiskHighBackgroundKey,
        RiskLevel.Critical => RiskCriticalBackgroundKey,
        _ => RiskMediumBackgroundKey,
    };

    /// <summary>Segoe MDL2 glyph per tool family; unknown tools get the puzzle piece.</summary>
    public static string Glyph(string toolId)
    {
        var id = toolId ?? string.Empty;

        if (Contains(id, "screen", "screenshot", "capture"))
        {
            return "\uE7B3";
        }

        if (Contains(id, "file", "dir", "folder"))
        {
            return "\uE8A5";
        }

        if (Contains(id, "process"))
        {
            return "\uE9D5";
        }

        if (Contains(id, "system"))
        {
            return "\uE770";
        }

        if (Contains(id, "app", "launch", "open"))
        {
            return "\uE7AD";
        }

        if (Contains(id, "shell", "command"))
        {
            return "\uE756";
        }

        return "\uEC7A";
    }

    private static bool Contains(string id, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (id.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
