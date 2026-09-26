using System;
using System.Linq;
using System.Threading.Tasks;
using OpenAgent.Transport;
using Xunit;

namespace OpenAgent.Transport.Tests;

public sealed class LocalLoopbackTransportTests
{
    [Fact]
    public async Task DiscoverAsync_returns_single_local_device()
    {
        var transport = new LocalLoopbackTransport();
        var devices = await transport.DiscoverAsync();

        Assert.Single(devices);
        var device = devices[0];
        Assert.True(device.IsOnline);
        Assert.Equal("loopback", device.ConnectionType);
        Assert.StartsWith("local:", device.Id, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DiscoverAsync_device_name_is_machine_name()
    {
        var transport = new LocalLoopbackTransport();
        var devices = await transport.DiscoverAsync();

        Assert.Equal(Environment.MachineName, devices[0].Name);
    }

    [Fact]
    public async Task DiscoverAsync_platform_is_windows()
    {
        var transport = new LocalLoopbackTransport();
        var devices = await transport.DiscoverAsync();

        Assert.Equal("Windows", devices[0].Platform);
    }

    [Fact]
    public async Task SendAsync_to_local_device_completes()
    {
        var transport = new LocalLoopbackTransport();
        var devices = await transport.DiscoverAsync();
        var localId = devices[0].Id;

        await transport.SendAsync(localId, Array.Empty<byte>());
    }

    [Fact]
    public async Task SendAsync_to_unknown_device_throws_NOT_IMPLEMENTED()
    {
        var transport = new LocalLoopbackTransport();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => transport.SendAsync("lan:some-other-box", Array.Empty<byte>()));
    }
}
