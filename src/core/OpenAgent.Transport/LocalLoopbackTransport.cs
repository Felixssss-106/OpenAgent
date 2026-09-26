namespace OpenAgent.Transport;

/// <summary>
/// Registers the local machine as the only device on the loopback transport
/// (spec sections 60-65). This is the Phase 6 floor: the shell always has at
/// least one real device to show before any LAN / relay adapter is written, so
/// the Devices page is never an empty shell or fake sample data.
/// </summary>
public sealed class LocalLoopbackTransport : ITransport
{
    /// <summary>
    /// Returns exactly one device — the local Windows host — always online over
    /// the loopback connection.
    /// </summary>
    public Task<IReadOnlyList<DeviceRecord>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var device = new DeviceRecord(
            Id: "local:" + Environment.MachineName,
            Name: Environment.MachineName,
            Platform: "Windows",
            Version: Environment.OSVersion.VersionString,
            ConnectionType: "loopback",
            IsOnline: true,
            Metrics: null);

        return Task.FromResult<IReadOnlyList<DeviceRecord>>(new[] { device });
    }

    /// <summary>
    /// The local device is a no-op target (there is no remote end to reach). Any
    /// non-local target is not implemented in the minimal seed.
    /// </summary>
    public Task SendAsync(string targetDeviceId, byte[] payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetDeviceId);
        cancellationToken.ThrowIfCancellationRequested();

        if (targetDeviceId.StartsWith("local:", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        throw new NotSupportedException("NOT IMPLEMENTED: non-local device transport");
    }
}
