using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OpenAgent.Transport;

/// <summary>The kind of cross-device message carried by <see cref="LanMessageEnvelope"/>.</summary>
public enum LanMessageType
{
    /// <summary>A request sent from one peer to another.</summary>
    Command,

    /// <summary>A reply; echoes the originating command's <see cref="LanMessageEnvelope.Id"/>.</summary>
    Result,

    /// <summary>A presence handshake beyond the UDP beacon.</summary>
    Hello,
}

/// <summary>
/// JSON message envelope for peer-to-peer commands and results over the LAN
/// (docs/protocol.md, section 3). It is the Phase 6 floor of the protocol layer
/// (spec §287): dependency-free so the Kotlin Android client and the .NET
/// Windows host speak the same wire format. <see cref="Decode"/> returns null on
/// anything that is not a well-formed envelope, so the listener can safely
/// ignore noise on the port.
/// </summary>
public sealed record LanMessageEnvelope(
    LanMessageType Type,
    string Id,
    string From,
    string To,
    string Text,
    long Ts)
{
    /// <summary>Serialises the envelope to a UTF-8 JSON payload.</summary>
    public byte[] Encode()
    {
        var node = new JsonObject
        {
            ["type"] = Type switch
            {
                LanMessageType.Command => "command",
                LanMessageType.Result => "result",
                _ => "hello",
            },
            ["id"] = Id,
            ["from"] = From,
            ["to"] = To,
            ["text"] = Text,
            ["ts"] = Ts,
        };

        return Encoding.UTF8.GetBytes(node.ToJsonString());
    }

    /// <summary>
    /// Parses a datagram as an envelope. Returns null when the bytes are not a
    /// JSON object with a recognised <c>type</c> plus <c>id</c>/<c>from</c>/<c>to</c>.
    /// </summary>
    public static LanMessageEnvelope? Decode(ReadOnlySpan<byte> bytes)
    {
        string text;
        try
        {
            text = Encoding.UTF8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        JsonElement root;
        try
        {
            using var doc = JsonDocument.Parse(text);
            root = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (!root.TryGetProperty("type", out var typeEl) || typeEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var type = typeEl.GetString() switch
        {
            "command" => LanMessageType.Command,
            "result" => LanMessageType.Result,
            "hello" => LanMessageType.Hello,
            _ => (LanMessageType?)null,
        };
        if (type is null)
        {
            return null;
        }

        if (!root.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        if (!root.TryGetProperty("from", out var fromEl) || fromEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        if (!root.TryGetProperty("to", out var toEl) || toEl.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var textVal = root.TryGetProperty("text", out var textEl) && textEl.ValueKind == JsonValueKind.String
            ? textEl.GetString()!
            : string.Empty;
        var ts = root.TryGetProperty("ts", out var tsEl) && tsEl.ValueKind == JsonValueKind.Number
            ? tsEl.GetInt64()
            : 0L;

        return new LanMessageEnvelope(type.Value, idEl.GetString()!, fromEl.GetString()!, toEl.GetString()!, textVal, ts);
    }
}

/// <summary>Event args carrying a decoded inbound envelope from a LAN peer.</summary>
public sealed class LanInboundMessageEventArgs : EventArgs
{
    public LanInboundMessageEventArgs(LanMessageEnvelope message) => Message = message;

    public LanMessageEnvelope Message { get; }
}
