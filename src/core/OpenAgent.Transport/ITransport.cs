namespace OpenAgent.Transport;

/// <summary>
/// Discovers devices and carries messages between them (spec sections 60-65).
/// Phase 6 minimal seed: only loopback is implemented; LAN / mDNS / relay throw
/// <see cref="NotSupportedException"/> ("NOT IMPLEMENTED") until their adapters
/// are written (Phase 6-7).
/// </summary>
public interface ITransport
{
    /// <summary>Devices currently visible to this transport.</summary>
    Task<IReadOnlyList<DeviceRecord>> DiscoverAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends a message to a device. The local device is a no-op target; any
    /// non-local target throws <see cref="NotSupportedException"/> in this seed.
    /// </summary>
    Task SendAsync(string targetDeviceId, byte[] payload, CancellationToken cancellationToken = default);
}
