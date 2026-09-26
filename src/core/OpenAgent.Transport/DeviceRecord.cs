namespace OpenAgent.Transport;

/// <summary>One device visible to the transport layer (spec sections 60-65).</summary>
public sealed record DeviceRecord(
    /// <summary>Stable device id, e.g. "local:" + MachineName.</summary>
    string Id,
    /// <summary>Display name (Environment.MachineName for the local host).</summary>
    string Name,
    /// <summary>"Windows" / "Android" etc.</summary>
    string Platform,
    /// <summary>OS version string.</summary>
    string Version,
    /// <summary>"loopback" / "lan" / "relay".</summary>
    string ConnectionType,
    /// <summary>Whether the device is currently reachable.</summary>
    bool IsOnline,
    /// <summary>Optional live metrics like "CPU 23% · RAM 51%".</summary>
    string? Metrics);
