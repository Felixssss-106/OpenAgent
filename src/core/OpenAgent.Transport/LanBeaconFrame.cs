using System;
using System.Globalization;
using System.Text;

namespace OpenAgent.Transport;

/// <summary>
/// Wire format for the LAN presence beacon (Phase 6, spec sections 60-65). A
/// beacon is a single UTF-8 line:
/// <c>OPENAGENT-BEACON v1|id|name|platform|version|port|ticks</c>.
/// The format is intentionally dependency-free and human-readable so it can be
/// unit-tested without a socket and extended later without breaking old peers.
/// Field values must not contain the pipe character; host names and OS versions
/// never do, so no escaping is needed for the seed.
/// </summary>
public sealed record LanBeaconFrame(
    /// <summary>Stable per-instance id, e.g. "lan:DESKTOP-AB12-c3f9".</summary>
    string DeviceId,
    /// <summary>Display name (machine name).</summary>
    string Name,
    /// <summary>"Windows" / "Android".</summary>
    string Platform,
    /// <summary>OS version string.</summary>
    string Version,
    /// <summary>The UDP port this peer listens on for beacons/messages.</summary>
    int Port,
    /// <summary>Environment.TickCount64 when the beacon was emitted (monotonic).</summary>
    long SentAtTicks)
{
    /// <summary>The magic prefix every beacon line starts with.</summary>
    public const string Prefix = "OPENAGENT-BEACON v1";

    /// <summary>Encodes the frame to a UTF-8 byte payload ready to send.</summary>
    public static byte[] Encode(LanBeaconFrame frame)
    {
        var line = string.Join(
            "|",
            Prefix,
            frame.DeviceId,
            frame.Name,
            frame.Platform,
            frame.Version,
            frame.Port.ToString(CultureInfo.InvariantCulture),
            frame.SentAtTicks.ToString(CultureInfo.InvariantCulture));
        return Encoding.UTF8.GetBytes(line);
    }

    /// <summary>
    /// Decodes a received datagram. Returns <see langword="null"/> for anything
    /// that is not a well-formed beacon (wrong prefix, field count, or numbers)
    /// so the listener can safely ignore noise on the port.
    /// </summary>
    public static LanBeaconFrame? Decode(ReadOnlySpan<byte> bytes)
    {
        string line;
        try
        {
            line = Encoding.UTF8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return null;
        }

        // A trailing newline is common when peers echo via line-oriented tools.
        if (line.Length > 0 && (line[^1] == '\n' || line[^1] == '\r'))
        {
            line = line.TrimEnd('\r', '\n');
        }

        var parts = line.Split('|');
        if (parts.Length != 7 || parts[0] != Prefix)
        {
            return null;
        }

        if (!int.TryParse(parts[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
        {
            return null;
        }

        if (!long.TryParse(parts[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks))
        {
            return null;
        }

        return new LanBeaconFrame(parts[1], parts[2], parts[3], parts[4], port, ticks);
    }
}
