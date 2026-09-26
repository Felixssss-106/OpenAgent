using System.Text;
using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Core.Error;
using OpenAgent.Security;
using OpenAgent.Tools;
using OpenAgent.Tools.Screen;

namespace OpenAgent.Tools.Tests;

/// <summary>
/// Screenshot tool (spec sections 45, 150). The capture is real GDI: the test
/// checks the PNG container the tool produced, not that it returned something.
/// </summary>
public sealed class ScreenCaptureToolTests
{
    private static readonly ToolContext Context = new()
    {
        PermissionMode = PermissionMode.ReadOnly,
    };

    [Fact]
    public async Task Capture_returns_a_real_png_of_the_screen()
    {
        var result = await new ScreenCaptureTool().ExecuteAsync(
            Context, Args("{}"), CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        var data = result.Data!.Value;
        Assert.Equal("png", data.GetProperty("format").GetString());

        var width = data.GetProperty("width").GetInt32();
        var height = data.GetProperty("height").GetInt32();
        Assert.True(width > 0 && height > 0);

        var bytes = Convert.FromBase64String(data.GetProperty("base64").GetString()!);
        Assert.Equal(data.GetProperty("sizeBytes").GetInt32(), bytes.Length);
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, bytes[..8]);
        Assert.Equal("IHDR", Encoding.ASCII.GetString(bytes[12..16]));
    }

    [Fact]
    public async Task Capture_honours_a_region()
    {
        var result = await new ScreenCaptureTool().ExecuteAsync(
            Context,
            Args("{\"mode\":\"region\",\"x\":0,\"y\":0,\"width\":120,\"height\":80}"),
            CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        var data = result.Data!.Value;
        Assert.Equal(120, data.GetProperty("width").GetInt32());
        Assert.Equal(80, data.GetProperty("height").GetInt32());
    }

    [Fact]
    public async Task Capture_scales_down_to_max_width()
    {
        var result = await new ScreenCaptureTool().ExecuteAsync(
            Context, Args("{\"maxWidth\":320}"), CancellationToken.None);

        Assert.True(result.Success, result.Error?.Message);
        var data = result.Data!.Value;
        Assert.Equal(320, data.GetProperty("width").GetInt32());
        Assert.True(data.GetProperty("height").GetInt32() > 0);
    }

    [Fact]
    public async Task Capture_rejects_an_empty_region()
    {
        var result = await new ScreenCaptureTool().ExecuteAsync(
            Context,
            Args("{\"mode\":\"region\",\"x\":0,\"y\":0,\"width\":0,\"height\":10}"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.ToolError(2), result.Error!.Code);
    }

    [Fact]
    public async Task Capture_rejects_an_unknown_mode()
    {
        var result = await new ScreenCaptureTool().ExecuteAsync(
            Context, Args("{\"mode\":\"hologram\"}"), CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(ErrorCodes.ToolError(2), result.Error!.Code);
    }

    [Fact]
    public void Capture_is_read_only_and_reversible()
    {
        var definition = new ScreenCaptureTool().Definition;

        Assert.Equal(RiskLevel.Low, definition.Risk);
        Assert.True(definition.Reversible);
        Assert.True(PermissionService.IsBatchApprovable(definition.Risk));
    }

    private static JsonElement Args(string json) => JsonDocument.Parse(json).RootElement;
}
