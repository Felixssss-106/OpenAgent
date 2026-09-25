using System.Text.Json;
using OpenAgent.Shared.Protocol;

namespace OpenAgent.Core.Tests;

public sealed class ProtocolSerializationTests
{
    [Fact]
    public void Envelope_round_trips_in_camel_case()
    {
        var payload = JsonSerializer.Deserialize<JsonElement>("{\"path\":\"D:/tmp\"}");
        var envelope = MessageEnvelope.Create(MessageTypes.CommandRequest, "android", "windows", payload, "session-1");

        var json = ProtocolJson.Serialize(envelope);
        var back = ProtocolJson.Deserialize<MessageEnvelope>(json);

        Assert.NotNull(back);
        Assert.Equal(envelope.MessageId, back!.MessageId);
        Assert.Equal(MessageTypes.CommandRequest, back.Type);
        Assert.Equal("android", back.Sender);
        Assert.Equal("windows", back.Receiver);
        Assert.Equal("session-1", back.SessionId);
        Assert.Equal(ProtocolVersion.Current, back.Version);
        Assert.Equal("D:/tmp", back.Payload!.Value.GetProperty("path").GetString());
    }

    [Fact]
    public void Json_uses_camel_case_property_names()
    {
        var json = ProtocolJson.Serialize(MessageEnvelope.Create(MessageTypes.Ping, "a", "b"));

        Assert.Contains("\"messageId\"", json, StringComparison.Ordinal);
        Assert.Contains("\"sender\"", json, StringComparison.Ordinal);
        Assert.Contains("\"timestamp\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_successful_result_carries_no_error()
    {
        var json = ProtocolJson.Serialize(ToolResult.Ok());

        Assert.DoesNotContain("error", json, StringComparison.Ordinal);
    }

    [Fact]
    public void A_failed_result_carries_a_code_and_no_data()
    {
        var json = ProtocolJson.Serialize(ToolResult.Fail("OA-5001", "Permission denied"));

        Assert.Contains("OA-5001", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"data\"", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(0, false)]
    public void Protocol_version_support_is_bounded(int version, bool expected) =>
        Assert.Equal(expected, ProtocolVersion.IsSupported(version));
}
