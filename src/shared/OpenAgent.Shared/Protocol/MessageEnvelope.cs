using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenAgent.Shared.Protocol;

/// <summary>
/// The single envelope every connection message uses (spec section 65).
/// </summary>
public sealed class MessageEnvelope
{
    public int Version { get; set; } = ProtocolVersion.Current;

    public string MessageId { get; set; } = Guid.NewGuid().ToString("N");

    public string? SessionId { get; set; }

    public string Type { get; set; } = string.Empty;

    public DateTimeOffset Timestamp { get; set; }

    public string Sender { get; set; } = string.Empty;

    public string Receiver { get; set; } = string.Empty;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public JsonElement? Payload { get; set; }

    public static MessageEnvelope Create(
        string type,
        string sender,
        string receiver,
        JsonElement? payload = null,
        string? sessionId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(type);
        return new MessageEnvelope
        {
            Type = type,
            Sender = sender,
            Receiver = receiver,
            Payload = payload,
            SessionId = sessionId,
            Timestamp = DateTimeOffset.UtcNow,
        };
    }
}
