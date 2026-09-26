namespace OpenAgent.Transport;

/// <summary>
/// Configuration for <see cref="UdpLanTransport"/>. Everything has a sensible
/// default via <see cref="Default"/>; tests pass an explicit <see cref="Port"/>
/// to avoid clashing with a running shell or other tests.
/// </summary>
public sealed record LanDiscoveryOptions(
    /// <summary>UDP port the beacon/message bus listens on. Default 47819.</summary>
    int Port,
    /// <summary>How often this host announces itself, in ms. Default 3000.</summary>
    int BeaconIntervalMs,
    /// <summary>
    /// Per-instance id. When null the transport derives a stable-enough id from
    /// the machine name plus a startup guid so two instances on one box get
    /// distinct ids and recognise each other.
    /// </summary>
    string? LocalId,
    /// <summary>Display name; null falls back to <see cref="Environment.MachineName"/>.</summary>
    string? LocalName,
    /// <summary>Platform string announced to peers. Default "Windows".</summary>
    string Platform,
    /// <summary>OS version announced to peers; null falls back to OSVersion.VersionString.</summary>
    string? Version)
{
    /// <summary>Production defaults (well-known port, 3s heartbeat).</summary>
    public static LanDiscoveryOptions Default() => new(
        Port: 47819,
        BeaconIntervalMs: 3000,
        LocalId: null,
        LocalName: null,
        Platform: "Windows",
        Version: null);
}
