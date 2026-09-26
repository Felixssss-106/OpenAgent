using System.Text;
using OpenAgent.Transport;
using Xunit;

namespace OpenAgent.Transport.Tests;

public sealed class LanBeaconFrameTests
{
    private static LanBeaconFrame Sample() => new(
        DeviceId: "lan:DESKTOP-AB12-c3f9",
        Name: "DESKTOP-AB12",
        Platform: "Windows",
        Version: "10.0.26100.0",
        Port: 47819,
        SentAtTicks: 1234567890);

    [Fact]
    public void Encode_then_Decode_round_trips()
    {
        var frame = Sample();
        var bytes = LanBeaconFrame.Encode(frame);
        var decoded = LanBeaconFrame.Decode(bytes);

        Assert.NotNull(decoded);
        Assert.Equal(frame, decoded);
    }

    [Fact]
    public void Encode_produces_the_v1_prefix()
    {
        var bytes = LanBeaconFrame.Encode(Sample());
        var line = Encoding.UTF8.GetString(bytes);

        Assert.StartsWith(LanBeaconFrame.Prefix, line, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Decode_returns_null_for_wrong_prefix()
    {
        var bytes = Encoding.UTF8.GetBytes("HELLO v2|lan:x|box|Windows|10|47819|1");
        Assert.Null(LanBeaconFrame.Decode(bytes));
    }

    [Fact]
    public void Decode_returns_null_for_wrong_field_count()
    {
        var bytes = Encoding.UTF8.GetBytes("OPENAGENT-BEACON v1|lan:x|box|Windows");
        Assert.Null(LanBeaconFrame.Decode(bytes));
    }

    [Fact]
    public void Decode_returns_null_for_non_numeric_port()
    {
        var bytes = Encoding.UTF8.GetBytes("OPENAGENT-BEACON v1|lan:x|box|Windows|10|port|1");
        Assert.Null(LanBeaconFrame.Decode(bytes));
    }

    [Fact]
    public void Decode_tolerates_a_trailing_newline()
    {
        var line = Encoding.UTF8.GetString(LanBeaconFrame.Encode(Sample())) + "\n";
        var decoded = LanBeaconFrame.Decode(Encoding.UTF8.GetBytes(line));

        Assert.NotNull(decoded);
        Assert.Equal(Sample().DeviceId, decoded!.DeviceId);
    }

    [Fact]
    public void Decode_returns_null_for_garbage()
    {
        Assert.Null(LanBeaconFrame.Decode(new byte[] { 0xFF, 0xFE, 0x00 }));
    }
}
