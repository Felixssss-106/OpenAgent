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

    /// <summary>Pairing step 1: the initiator offers its identity key (v2, docs/protocol.md §4).</summary>
    PairRequest,

    /// <summary>Pairing step 2: the host answers with its identity key after the user accepts the PIN.</summary>
    PairChallenge,

    /// <summary>Pairing step 3: the initiator proves it derived the same key.</summary>
    PairConfirm,

    /// <summary>Pairing final: the host accepts the proof and both store the key.</summary>
    PairComplete,

    /// <summary>A task on the host needs a decision (v2, encrypted; phase 8 of the app).</summary>
    ApprovalRequest,

    /// <summary>The phone's decision on an <see cref="LanMessageType.ApprovalRequest"/>.</summary>
    ApprovalResolve,
}

/// <summary>
/// JSON message envelope for peer-to-peer commands and results over the LAN
/// (docs/protocol.md, section 3). It is the Phase 6 floor of the protocol layer
/// (spec §287): dependency-free so the Kotlin Android client and the .NET
/// Windows host speak the same wire format. <see cref="Decode"/> returns null on
/// anything that is not a well-formed envelope, so the listener can safely
/// ignore noise on the port. v2 adds the pairing types and the optional
/// <c>pub</c>/<c>nonce</c>/<c>cipher</c> fields (§4); a v1 peer simply never
/// emits them and refuses what it cannot read.
/// </summary>
public sealed record LanMessageEnvelope(
    LanMessageType Type,
    string Id,
    string From,
    string To,
    string Text,
    long Ts,
    string? Pub = null,
    string? Nonce = null,
    string? Cipher = null)
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
                LanMessageType.Hello => "hello",
                LanMessageType.PairRequest => "pair_request",
                LanMessageType.PairChallenge => "pair_challenge",
                LanMessageType.PairConfirm => "pair_confirm",
                LanMessageType.PairComplete => "pair_complete",
                LanMessageType.ApprovalRequest => "approval_request",
                LanMessageType.ApprovalResolve => "approval_resolve",
                _ => "hello",
            },
            ["id"] = Id,
            ["from"] = From,
            ["to"] = To,
            ["text"] = Text,
            ["ts"] = Ts,
        };

        if (Pub is not null)
        {
            node["pub"] = Pub;
        }

        if (Nonce is not null)
        {
            node["nonce"] = Nonce;
        }

        if (Cipher is not null)
        {
            node["cipher"] = Cipher;
        }

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
            "pair_request" => LanMessageType.PairRequest,
            "pair_challenge" => LanMessageType.PairChallenge,
            "pair_confirm" => LanMessageType.PairConfirm,
            "pair_complete" => LanMessageType.PairComplete,
            "approval_request" => LanMessageType.ApprovalRequest,
            "approval_resolve" => LanMessageType.ApprovalResolve,
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

        var pub = root.TryGetProperty("pub", out var pubEl) && pubEl.ValueKind == JsonValueKind.String
            ? pubEl.GetString()
            : null;
        var nonce = root.TryGetProperty("nonce", out var nonceEl) && nonceEl.ValueKind == JsonValueKind.String
            ? nonceEl.GetString()
            : null;
        var cipher = root.TryGetProperty("cipher", out var cipherEl) && cipherEl.ValueKind == JsonValueKind.String
            ? cipherEl.GetString()
            : null;

        return new LanMessageEnvelope(type.Value, idEl.GetString()!, fromEl.GetString()!, toEl.GetString()!, textVal, ts, pub, nonce, cipher);
    }
}

/// <summary>Event args carrying a decoded inbound envelope from a LAN peer.</summary>
public sealed class LanInboundMessageEventArgs : EventArgs
{
    public LanInboundMessageEventArgs(LanMessageEnvelope message) => Message = message;

    public LanMessageEnvelope Message { get; }
}
