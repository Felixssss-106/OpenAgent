using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using OpenAgent.Transport;
using Xunit;

namespace OpenAgent.Transport.Tests;

public sealed class UdpLanTransportTests : IDisposable
{
    private UdpLanTransport? _transport;

    public void Dispose()
    {
        _transport?.Dispose();
    }

    private static int GetFreeUdpPort()
    {
        using var probe = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)probe.Client.LocalEndPoint!).Port;
    }

    [Fact]
    public async Task DiscoverAsync_always_includes_the_local_loopback_device()
    {
        var transport = new UdpLanTransport(
            LanDiscoveryOptions.Default() with { Port = GetFreeUdpPort() },
            new LocalLoopbackTransport());
        _transport = transport;

        var devices = await transport.DiscoverAsync();

        Assert.Contains(devices, d => d.ConnectionType == "loopback");
    }

    [Fact]
    public async Task Degrades_to_loopback_only_when_the_port_is_taken()
    {
        // Reserve the port so the transport's bind fails and it degrades silently.
        using var reserved = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
        var takenPort = ((IPEndPoint)reserved.Client.LocalEndPoint!).Port;

        var transport = new UdpLanTransport(
            LanDiscoveryOptions.Default() with { Port = takenPort },
            new LocalLoopbackTransport());
        _transport = transport;

        var devices = await transport.DiscoverAsync();

        Assert.Single(devices);
        Assert.Equal("loopback", devices[0].ConnectionType);
    }

    [Fact]
    public async Task SendAsync_to_local_device_is_a_noop_and_does_not_throw()
    {
        var transport = new UdpLanTransport(
            LanDiscoveryOptions.Default() with { Port = GetFreeUdpPort() },
            new LocalLoopbackTransport());
        _transport = transport;

        // Must not throw NOT IMPLEMENTED like the loopback-only seed did.
        await transport.SendAsync("local:host", Array.Empty<byte>());
    }

    [Fact]
    public async Task Discovers_a_real_peer_over_udp()
    {
        var port = GetFreeUdpPort();
        var transport = new UdpLanTransport(
            LanDiscoveryOptions.Default() with { Port = port, BeaconIntervalMs = 200 },
            new LocalLoopbackTransport());
        _transport = transport;

        const string peerId = "lan:peer-integration-1";
        var beacon = LanBeaconFrame.Encode(new LanBeaconFrame(
            peerId, "PeerBox", "Windows", "10.0.26100.0", port, Environment.TickCount64));

        using var sender = new UdpClient();
        sender.Send(beacon, beacon.Length, new IPEndPoint(IPAddress.Loopback, port));

        // Poll until the listener caches the peer (or time out).
        var peer = await WaitForPeerAsync(transport, peerId, TimeSpan.FromSeconds(2));

        Assert.NotNull(peer);
        Assert.Equal("lan", peer!.ConnectionType);
        Assert.Equal("PeerBox", peer.Name);
        Assert.True(peer.IsOnline);
    }

    [Fact]
    public async Task SendAsync_delivers_payload_to_a_known_peer_endpoint()
    {
        var port = GetFreeUdpPort();
        var transport = new UdpLanTransport(
            LanDiscoveryOptions.Default() with { Port = port, BeaconIntervalMs = 200 },
            new LocalLoopbackTransport());
        _transport = transport;

        const string peerId = "lan:peer-integration-2";
        var beacon = LanBeaconFrame.Encode(new LanBeaconFrame(
            peerId, "PeerBox", "Windows", "10.0.26100.0", port, Environment.TickCount64));

        using var sender = new UdpClient();
        sender.Send(beacon, beacon.Length, new IPEndPoint(IPAddress.Loopback, port));

        var peer = await WaitForPeerAsync(transport, peerId, TimeSpan.FromSeconds(2));
        Assert.NotNull(peer);

        var payload = new byte[] { 9, 8, 7, 6, 5 };
        await transport.SendAsync(peerId, payload);

        var received = await ReceiveIgnoreBeaconsAsync(sender, payload, TimeSpan.FromSeconds(2));
        Assert.NotNull(received);
        Assert.Equal(payload, received);
    }

    private static async Task<DeviceRecord?> WaitForPeerAsync(
        UdpLanTransport transport, string peerId, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        while (!cts.IsCancellationRequested)
        {
            var devices = await transport.DiscoverAsync();
            var found = devices.FirstOrDefault(d => d.Id == peerId);
            if (found is not null)
            {
                return found;
            }

            await Task.Delay(50, cts.Token);
        }

        return null;
    }

    private static async Task<byte[]?> ReceiveIgnoreBeaconsAsync(
        UdpClient client, byte[] expected, TimeSpan timeout)
    {
        using var cts = new CancellationTokenSource(timeout);
        var receiveTask = client.ReceiveAsync();
        while (!cts.IsCancellationRequested)
        {
            var delay = Task.Delay(50, cts.Token);
            var completed = await Task.WhenAny(receiveTask, delay);
            if (completed == delay)
            {
                // Timed out this round (or cancelled); loop exits if cancelled.
                continue;
            }

            var result = await receiveTask;
            if (result.Buffer.Length == expected.Length && result.Buffer.SequenceEqual(expected))
            {
                return result.Buffer;
            }

            // Ignore beacon frames (noise) and keep waiting for the payload.
            receiveTask = client.ReceiveAsync();
        }

        return null;
    }
}
