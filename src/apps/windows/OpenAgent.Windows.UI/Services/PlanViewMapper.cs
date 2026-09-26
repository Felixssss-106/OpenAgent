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
}
