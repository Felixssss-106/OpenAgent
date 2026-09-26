using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace OpenAgent.Windows.UI.Services;

/// <summary>
/// Turns what the tool registry consumes into what the artboards draw.
/// </summary>
public static class PlanViewMapper
{
    /// <summary>
    /// Artboard 05 shows <c>D:\Downloads\* → D:\Downloads\2026-09-25\</c> on the
    /// approval card and 03/04 show <c>file.list  D:\Downloads</c> in the tool
    /// disclosure row, so the JSON braces and quotes never reach the surface.
    /// A source/destination pair reads as a move; every other shape keeps its key,
    /// because a two-argument write must not look like one path becoming another.
    /// Unparseable input is returned as-is: the parameters are the one thing the
    /// user is being asked to confirm, so showing them raw beats showing nothing.
    /// </summary>
    public static string DescribeArguments(string argumentsJson)
    {
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return argumentsJson;
        }
        catch (ArgumentException)
        {
            return argumentsJson;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return argumentsJson;
        }

        var args = root.EnumerateObject()
            .Where(p => p.Value.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array))
            .Select(p => (p.Name, Value: Describe(p.Value)))
            .ToList();

        if (args.Count == 0)
        {
            return "无参数";
        }

        // Artboards 03/04 draw the disclosure row as "file.list  D:\Downloads": one
        // argument is named by the tool itself, so repeating the key adds nothing.
        if (args.Count == 1)
        {
            return args[0].Value;
        }

        if (args.Count == 2 && args[0].Name == "source" && args[1].Name == "destination")
        {
            return $"{args[0].Value} → {args[1].Value}";
        }

        return string.Join("  ·  ", args.Select(a => $"{a.Name}：{a.Value}"));

        static string Describe(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.True => "是",
            JsonValueKind.False => "否",
            _ => element.GetRawText(),
        };
    }

    /// <summary>
    /// The result block as artboards 03/04 draw it: a short plain-text line per fact,
    /// which is also what the block's own 纯文本 label promises. The payload a tool
    /// returns is JSON, and one long line of it neither reads nor wraps — it truncates
    /// mid-value, so the last fact shown is a partial one.
    /// </summary>
    public static string DescribeResult(string? dataJson, int maxLines = 12)
    {
        if (string.IsNullOrWhiteSpace(dataJson))
        {
            return "完成";
        }

        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(dataJson);
            root = document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return dataJson;
        }
        catch (ArgumentException)
        {
            return dataJson;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return Inline(root);
        }

        var lines = new List<string>();
        foreach (var property in root.EnumerateObject())
        {
            if (lines.Count >= maxLines)
            {
                lines.Add("…");
                break;
            }

            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Array:
                    var items = property.Value.EnumerateArray().ToList();
                    lines.Add($"{property.Name}：{items.Count} 项");
                    foreach (var item in items.Take(3))
                    {
                        if (lines.Count >= maxLines)
                        {
                            break;
                        }

                        lines.Add($"  · {Inline(item)}");
                    }

                    if (items.Count > 3 && lines.Count < maxLines)
                    {
                        lines.Add($"  …共 {items.Count} 项");
                    }

                    break;

                case JsonValueKind.Object:
                    lines.Add($"{property.Name}：{Inline(property.Value)}");
                    break;

                default:
                    lines.Add($"{property.Name}：{Scalar(property.Value)}");
                    break;
            }
        }

        return string.Join("\n", lines);
    }

    /// <summary>One line for a value that is not itself a flat fact.</summary>
    private static string Inline(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Object => string.Join(
            " · ",
            element.EnumerateObject().Select(p => $"{p.Name}：{Scalar(p.Value)}")),
        JsonValueKind.Array => $"{element.GetArrayLength()} 项",
        _ => Scalar(element),
    };

    private static string Scalar(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.True => "是",
        JsonValueKind.False => "否",
        JsonValueKind.Null => "—",
        _ => element.GetRawText(),
    };
}
