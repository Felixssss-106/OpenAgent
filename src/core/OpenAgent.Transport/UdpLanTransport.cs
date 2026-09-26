using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace OpenAgent.Transport;

/// <summary>
/// UDP-based LAN discovery and messaging (Phase 6 increment, spec sections 60-65).
/// It composes the <see cref="LocalLoopbackTransport"/> so the local host is
/// always present, then broadcasts a presence beacon and listens for peers on a
/// well-known UDP port. Discovered peers show up as <c>ConnectionType = "lan"</c>
/// devices. If the socket cannot bind (port taken, no permission, headless),
/// the transport degrades silently to loopback-only — discovery and the shell
/// keep working, LAN just stays empty.
/// </summary>
/// <remarks>
/// Message delivery is a seed: <see cref="SendAsync"/> for a known peer sends a
/// unicast datagram to its last-seen endpoint, otherwise a best-effort broadcast.
/// Per-device routing, pairing and encryption arrive in Phase 6-7.
/// </remarks>
public sealed class UdpLanTransport : ITransport, IDisposable
{
    private readonly ITransport _loopback;
    private readonly LanDiscoveryOptions _options;
    private readonly string _localId;
    private readonly string _localName;
    private readonly string _platform;
    private readonly string _version;

    private readonly UdpClient? _socket;
    private readonly CancellationTokenSource _cts = new();
    private readonly Timer? _beaconTimer;
    private readonly Task? _listenerTask;

    // deviceId -> (frame, lastSeenUtc, endpoint)
    private readonly ConcurrentDictionary<string, (LanBeaconFrame Frame, DateTime Seen, IPEndPoint Endpoint)> _peers =
        new(StringComparer.Ordinal);

    private bool _disposed;

    /// <summary>
    /// A peer is dropped if not heard from within this multiple of the beacon
    /// interval — covers a peer going offline without a goodbye.
    /// </summary>
    private TimeSpan PeerExpiry => TimeSpan.FromMilliseconds(_options.BeaconIntervalMs * 3);

    public UdpLanTransport(LanDiscoveryOptions options, ITransport loopback)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(loopback);

        _loopback = loopback;
        _options = options;
        _localId = options.LocalId
                   ?? $"lan:{Environment.MachineName}-{Guid.NewGuid():N}";
        _localName = options.LocalName ?? Environment.MachineName;
        _platform = options.Platform;
        _version = options.Version ?? Environment.OSVersion.VersionString;

        try
        {
            _socket = new UdpClient(new IPEndPoint(IPAddress.Any, options.Port))
            {
                EnableBroadcast = true,
            };

            _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
            _beaconTimer = new Timer(_ => SendBeacon(), null, 0, options.BeaconIntervalMs);
        }
        catch (SocketException)
        {
            // Degraded: no LAN, loopback only. Nothing throws.
            _socket = null;
        }
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DeviceRecord>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var local = await _loopback.DiscoverAsync(cancellationToken);

        var now = DateTime.UtcNow;
        var peers = _peers
            .Where(p => now - p.Value.Seen <= PeerExpiry)
            .Select(p => new DeviceRecord(
                Id: p.Value.Frame.DeviceId,
                Name: p.Value.Frame.Name,
                Platform: p.Value.Frame.Platform,
                Version: p.Value.Frame.Version,
                ConnectionType: "lan",
                IsOnline: true,
                Metrics: null))
            .ToArray();

        return local.Concat(peers).ToArray();
    }

    /// <inheritdoc/>
    public Task SendAsync(string targetDeviceId, byte[] payload, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(targetDeviceId);
        ArgumentNullException.ThrowIfNull(payload);
        cancellationToken.ThrowIfCancellationRequested();

        if (targetDeviceId.StartsWith("local:", StringComparison.OrdinalIgnoreCase))
        {
            return Task.CompletedTask;
        }

        if (_socket is null)
        {
            // Degraded: cannot reach the LAN.
            return Task.CompletedTask;
        }

        try
        {
            if (_peers.TryGetValue(targetDeviceId, out var peer))
            {
                _socket.Send(payload, payload.Length, peer.Endpoint);
            }
            else
            {
                _socket.Send(payload, payload.Length, new IPEndPoint(IPAddress.Broadcast, _options.Port));
            }
        }
        catch (SocketException)
        {
            // Best-effort seed: a failed send must not crash the caller.
        }
        catch (ObjectDisposedException)
        {
            // Transport is shutting down.
        }

        return Task.CompletedTask;
    }

    private void SendBeacon()
    {
        if (_socket is null)
        {
            return;
        }

        var frame = new LanBeaconFrame(
            _localId, _localName, _platform, _version, _options.Port, Environment.TickCount64);
        byte[] bytes = LanBeaconFrame.Encode(frame);

        try
        {
            // Broadcast reaches other machines; loopback reaches peers on this host.
            _socket.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Broadcast, _options.Port));
            _socket.Send(bytes, bytes.Length, new IPEndPoint(IPAddress.Loopback, _options.Port));
        }
        catch (SocketException)
        {
            // Best-effort.
        }
        catch (ObjectDisposedException)
        {
            // Shutting down.
        }
    }

    private async Task ListenLoopAsync(CancellationToken token)
    {
        if (_socket is null)
        {
            return;
        }

        try
        {
            while (!token.IsCancellationRequested)
            {
                UdpReceiveResult result;
                try
                {
                    result = await _socket.ReceiveAsync(token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (SocketException)
                {
                    // Transient socket error; keep listening unless we're stopping.
                    if (token.IsCancellationRequested)
                    {
                        break;
                    }

                    continue;
                }

                var frame = LanBeaconFrame.Decode(result.Buffer);
                if (frame is null)
                {
                    continue;
                }

                if (frame.DeviceId == _localId)
                {
                    continue; // ignore our own beacon
                }

                _peers[frame.DeviceId] = (frame, DateTime.UtcNow, result.RemoteEndPoint);
            }
        }
        catch (OperationCanceledException)
        {
            // Expected on shutdown.
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _cts.Cancel();
        _beaconTimer?.Dispose();
        try
        {
            _socket?.Close();
        }
        catch (SocketException)
        {
            // ignore
        }

        _cts.Dispose();
    }
}
