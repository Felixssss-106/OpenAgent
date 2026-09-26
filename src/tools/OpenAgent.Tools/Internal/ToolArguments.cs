using System.Text.Json;

namespace OpenAgent.Tools.Internal;

/// <summary>
/// Argument readers shared by every tool. A model's arguments are untrusted
/// input: they are read defensively and never assumed to have the shape the
/// schema promised (spec section 131).
/// </summary>
internal static class ToolArguments
{
    internal static string? ReadString(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty(name, out var node) ||
            node.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return node.GetString();
    }

    internal static bool ReadBoolean(JsonElement arguments, string name, bool fallback = false)
    {
        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty(name, out var node))
        {
            return fallback;
        }

        return node.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => fallback,
        };
    }

    internal static int? ReadInt32(JsonElement arguments, string name)
    {
        if (arguments.ValueKind != JsonValueKind.Object ||
            !arguments.TryGetProperty(name, out var node) ||
            node.ValueKind != JsonValueKind.Number ||
            !node.TryGetInt32(out var value))
        {
            return null;
        }

        return value;
    }
}
