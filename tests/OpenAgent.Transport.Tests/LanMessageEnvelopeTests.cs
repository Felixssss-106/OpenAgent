using System.Text;
using OpenAgent.Transport;
using Xunit;

namespace OpenAgent.Transport.Tests;

public sealed class LanMessageEnvelopeTests
{
    private static LanMessageEnvelope Sample() => new(
        Type: LanMessageType.Command,
        Id: "f47ac10b-58cc-4372-a567-0e02b2c3d479",
        From: "android:Pixel-9-1a2b",
        To: "lan:DESKTOP-AB12-c3f9",
        Text: "open notepad",
        Ts: 1716820000000);

    [Fact]
    public void Encode_then_Decode_round_trips()
    {
        var envelope = Sample();
        var decoded = LanMessageEnvelope.Decode(envelope.Encode());

        Assert.NotNull(decoded);
        Assert.Equal(envelope, decoded);
    }

    [Fact]
    public void Decode_parses_all_message_types()
    {
        Assert.Equal(LanMessageType.Command, LanMessageEnvelope.Decode(new LanMessageEnvelope(LanMessageType.Command, "1", "a", "b", "", 0).Encode())!.Type);
        Assert.Equal(LanMessageType.Result, LanMessageEnvelope.Decode(new LanMessageEnvelope(LanMessageType.Result, "1", "a", "b", "", 0).Encode())!.Type);
        Assert.Equal(LanMessageType.Hello, LanMessageEnvelope.Decode(new LanMessageEnvelope(LanMessageType.Hello, "1", "a", "b", "", 0).Encode())!.Type);
    }

    [Fact]
    public void Decode_returns_null_for_non_json()
    {
        Assert.Null(LanMessageEnvelope.Decode(Encoding.UTF8.GetBytes("OPENAGENT-BEACON v1|x|y|Windows|10|47819|1")));
    }

    [Fact]
    public void Decode_returns_null_for_json_missing_required_fields()
    {
        var json = Encoding.UTF8.GetBytes("{\"type\":\"command\"}");
        Assert.Null(LanMessageEnvelope.Decode(json));
    }

    [Fact]
    public void Decode_returns_null_for_unknown_type()
    {
        var json = Encoding.UTF8.GetBytes("{\"type\":\"explode\",\"id\":\"1\",\"from\":\"a\",\"to\":\"b\"}");
        Assert.Null(LanMessageEnvelope.Decode(json));
    }

    [Fact]
    public void Decode_tolerates_missing_text_and_ts()
    {
        var json = Encoding.UTF8.GetBytes("{\"type\":\"command\",\"id\":\"1\",\"from\":\"a\",\"to\":\"b\"}");
        var decoded = LanMessageEnvelope.Decode(json);

        Assert.NotNull(decoded);
        Assert.Equal(string.Empty, decoded!.Text);
        Assert.Equal(0L, decoded.Ts);
    }
}
