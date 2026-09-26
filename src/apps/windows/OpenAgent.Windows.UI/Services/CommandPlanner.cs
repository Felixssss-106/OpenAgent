using System.Collections.Generic;
using System.Text.Json;

namespace OpenAgent.Windows.UI.Services;

/// <summary>One interpreted command: which tool, with which arguments.</summary>
public sealed record CommandPlan(string ToolId, string ArgumentsJson, string Rationale);

/// <summary>
/// Turns one line of user text into a single tool call. This is the stand-in
/// for the provider's function-calling step: until a model is wired up the
/// routing is deterministic, so behaviour is reproducible and testable instead
/// of random (spec sections 132, 152).
/// </summary>
public static class CommandPlanner
{
    private static readonly string[] LaunchKeywords = { "打开", "启动", "运行", "launch", "open", "start" };
    private static readonly string[] ProcessKeywords = { "进程", "process", "tasklist" };
    private static readonly string[] SystemKeywords = { "系统", "内存", "内存占用", "cpu", "性能", "配置", "system", "info" };

    /// <summary>Null means "no registered tool matches", and the caller says so.</summary>
    public static CommandPlan? Plan(string? prompt, IReadOnlyList<ToolSummary> tools)
    {
        if (string.IsNullOrWhiteSpace(prompt) || tools is null || tools.Count == 0)
        {
            return null;
        }

        var text = prompt.Trim();

        if (ContainsAny(text, LaunchKeywords))
        {
            var launchTool = Prefer(tools, "app.launch", "launch", "app");
            var target = ExtractTarget(text);
            if (launchTool is not null && target.Length > 0)
            {
                return new CommandPlan(
                    launchTool.Id,
                    JsonSerializer.Serialize(new Dictionary<string, string> { ["target"] = target }),
                    $"启动 {target}");
            }
        }

        if (ContainsAny(text, ProcessKeywords))
        {
            var processTool = Prefer(tools, "process.list", "process");
            if (processTool is not null)
            {
                return new CommandPlan(processTool.Id, "{}", "列出运行中的进程");
            }
        }

        if (ContainsAny(text, SystemKeywords))
        {
            var systemTool = Prefer(tools, "system.get_info", "system");
            if (systemTool is not null)
            {
                return new CommandPlan(systemTool.Id, "{}", "读取系统信息");
            }
        }

        return null;
    }

    /// <summary>Text following the first launch keyword, with trailing punctuation removed.</summary>
    public static string ExtractTarget(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        foreach (var keyword in LaunchKeywords)
        {
            var index = text.IndexOf(keyword, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                continue;
            }

            var rest = text[(index + keyword.Length)..].Trim();
            // Iteratively strip punctuation and whitespace from both ends —
            // removing one may expose the other (e.g. `"spotify" 的` →
            // `spotify" ` after 的 is removed, then `"` and space are stripped).
            while (true)
            {
                var prev = rest;
                rest = rest.Trim('。', '.', '，', ',', '；', ';', '！', '!', '？', '?', '"', '\'', '“', '”', '的').Trim();
                if (rest.Length == prev.Length)
                {
                    break;
                }
            }
            if (rest.Length > 0)
            {
                return rest;
            }
        }

        return string.Empty;
    }

    private static ToolSummary? Prefer(IReadOnlyList<ToolSummary> tools, params string[] preferences)
    {
        foreach (var preference in preferences)
        {
            foreach (var tool in tools)
            {
                if (tool.Id.Contains(preference, StringComparison.OrdinalIgnoreCase))
                {
                    return tool;
                }
            }
        }

        return null;
    }

    private static bool ContainsAny(string text, string[] keywords)
    {
        foreach (var keyword in keywords)
        {
            if (text.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
