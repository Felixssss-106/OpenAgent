using System.ComponentModel;
using System.Text.Json;
using OpenAgent.Core;
using OpenAgent.Core.Error;
using OpenAgent.Security;
using OpenAgent.Shared.Protocol;
using OpenAgent.Tools.Internal;

namespace OpenAgent.Tools.Screen;

/// <summary>
/// Read-only screenshot: full screen, foreground window or a region
/// (spec sections 45, 150, 298). The bitmap never leaves the machine through
/// this tool — it is returned to the caller, and sending it to a provider is a
/// separate, explicitly approved step (spec section 357).
/// </summary>
public sealed class ScreenCaptureTool : ITool
{
    private const int MinPixels = 8;

    private const int MaxPixels = 16384;

    private const int MinScaledWidth = 16;

    public ToolDefinition Definition { get; } = new()
    {
        Id = "screen.capture",
        Name = "Capture screenshot",
        Description = "Captures the screen, the foreground window or a region and returns a PNG.",
        InputSchemaJson = """
        {"type":"object","properties":{"mode":{"type":"string"},"x":{"type":"integer"},"y":{"type":"integer"},"width":{"type":"integer"},"height":{"type":"integer"},"maxWidth":{"type":"integer"}},"required":[]}
        """,
        Risk = RiskLevel.Low,
        Permissions = new[] { "screen.read" },
        Reversible = true,
        RequiresApproval = false,
    };

    public Task<ToolResult> ExecuteAsync(
        ToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            return Task.FromResult(ToolResult.Fail(
                ErrorCodes.ToolError(8), "屏幕截图仅支持 Windows"));
        }

        var mode = ToolArguments.ReadString(arguments, "mode");
        if (string.IsNullOrWhiteSpace(mode))
        {
            mode = "screen";
        }

        ScreenRect? region = null;
        switch (mode.ToLowerInvariant())
        {
            case "screen":
                region = GdiScreenCapture.VirtualScreen();
                break;

            case "window":
                region = GdiScreenCapture.ForegroundWindow();
                if (region is null)
                {
                    return Task.FromResult(ToolResult.Fail(
                        ErrorCodes.ToolError(8), "没有活动窗口可捕获"));
                }

                break;

            case "region":
                var x = ToolArguments.ReadInt32(arguments, "x");
                var y = ToolArguments.ReadInt32(arguments, "y");
                var width = ToolArguments.ReadInt32(arguments, "width");
                var height = ToolArguments.ReadInt32(arguments, "height");

                if (x is null || y is null || width is null || height is null)
                {
                    return Task.FromResult(ToolResult.Fail(
                        ErrorCodes.ToolError(2), "region 模式需要 x、y、width、height"));
                }

                if (width <= 0 || height <= 0)
                {
                    return Task.FromResult(ToolResult.Fail(
                        ErrorCodes.ToolError(2), "width 与 height 必须为正整数"));
                }

                region = new ScreenRect(x.Value, y.Value, width.Value, height.Value);
                break;

            default:
                return Task.FromResult(ToolResult.Fail(
                    ErrorCodes.ToolError(2), $"未知的捕获模式：{mode}"));
        }

        var clamped = Clamp(region.Value, GdiScreenCapture.VirtualScreen());
        if (clamped.Width < MinPixels || clamped.Height < MinPixels ||
            clamped.Width > MaxPixels || clamped.Height > MaxPixels)
        {
            return Task.FromResult(ToolResult.Fail(
                ErrorCodes.ToolError(2),
                $"捕获区域尺寸无效：{clamped.Width}×{clamped.Height}"));
        }

        byte[] png;
        int finalWidth = clamped.Width;
        int finalHeight = clamped.Height;

        try
        {
            var rgb = GdiScreenCapture.CaptureRgb(clamped);

            var maxWidth = ToolArguments.ReadInt32(arguments, "maxWidth") ?? 0;
            if (maxWidth > 0)
            {
                maxWidth = Math.Clamp(maxWidth, MinScaledWidth, MaxPixels);
                var scaled = PngEncoder.Downscale(rgb, clamped.Width, clamped.Height, maxWidth);
                rgb = scaled.Pixels;
                finalWidth = scaled.Width;
                finalHeight = scaled.Height;
            }

            png = PngEncoder.Encode(rgb, finalWidth, finalHeight);
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException or ArgumentException)
        {
            return Task.FromResult(ToolResult.Fail(
                ErrorCodes.ToolError(8), $"截图失败：{SecretRedactor.Redact(ex.Message)}"));
        }

        return Task.FromResult(ToolResult.Ok(JsonSerializer.SerializeToElement(new Dictionary<string, object?>
        {
            ["mode"] = mode.ToLowerInvariant(),
            ["format"] = "png",
            ["width"] = finalWidth,
            ["height"] = finalHeight,
            ["sizeBytes"] = png.Length,
            ["base64"] = Convert.ToBase64String(png),
            ["capturedAtUtc"] = DateTimeOffset.UtcNow,
        })));
    }

    /// <summary>Intersects the requested region with the virtual screen.</summary>
    private static ScreenRect Clamp(ScreenRect region, ScreenRect screen)
    {
        var left = Math.Max(region.X, screen.X);
        var top = Math.Max(region.Y, screen.Y);
        var right = Math.Min(region.X + region.Width, screen.X + screen.Width);
        var bottom = Math.Min(region.Y + region.Height, screen.Y + screen.Height);

        return new ScreenRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
    }
}
