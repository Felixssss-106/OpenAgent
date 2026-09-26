using System.ComponentModel;
using System.Runtime.InteropServices;

namespace OpenAgent.Tools.Screen;

/// <summary>Axis-aligned screen rectangle in virtual-screen coordinates.</summary>
internal readonly record struct ScreenRect(int X, int Y, int Width, int Height);

/// <summary>
/// Full-screen, foreground-window and region capture on top of GDI.
/// All native handles are released in <c>finally</c>: a screenshot tool that
/// leaks a DC per call would degrade the whole session (spec section 150).
/// </summary>
internal static class GdiScreenCapture
{
    private const uint SrcCopy = 0x00CC0020;

    private const uint DibRgbColors = 0;

    private const int BitmapInfoHeaderSize = 40;

    private const int RectSize = 16;

    /// <summary>Bounding box of every monitor, or the primary one when the virtual metrics are unavailable.</summary>
    internal static ScreenRect VirtualScreen()
    {
        var x = NativeMethods.GetSystemMetrics(NativeMethods.SmXVirtualScreen);
        var y = NativeMethods.GetSystemMetrics(NativeMethods.SmYVirtualScreen);
        var width = NativeMethods.GetSystemMetrics(NativeMethods.SmCxVirtualScreen);
        var height = NativeMethods.GetSystemMetrics(NativeMethods.SmCyVirtualScreen);

        if (width <= 0 || height <= 0)
        {
            return new ScreenRect(
                0,
                0,
                NativeMethods.GetSystemMetrics(NativeMethods.SmCxScreen),
                NativeMethods.GetSystemMetrics(NativeMethods.SmCyScreen));
        }

        return new ScreenRect(x, y, width, height);
    }

    /// <summary>Returns null when there is no foreground window (locked screen, session 0).</summary>
    internal static ScreenRect? ForegroundWindow()
    {
        var window = NativeMethods.GetForegroundWindow();
        if (window == IntPtr.Zero)
        {
            return null;
        }

        var rect = Marshal.AllocHGlobal(RectSize);
        try
        {
            if (!NativeMethods.GetWindowRect(window, rect))
            {
                return null;
            }

            var left = Marshal.ReadInt32(rect, 0);
            var top = Marshal.ReadInt32(rect, 4);
            var right = Marshal.ReadInt32(rect, 8);
            var bottom = Marshal.ReadInt32(rect, 12);

            return new ScreenRect(left, top, Math.Max(0, right - left), Math.Max(0, bottom - top));
        }
        finally
        {
            Marshal.FreeHGlobal(rect);
        }
    }

    /// <summary>Captures <paramref name="region"/> and returns top-down 24-bit RGB pixels.</summary>
    internal static byte[] CaptureRgb(ScreenRect region)
    {
        if (region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(region), "捕获区域为空");
        }

        var screenContext = NativeMethods.GetDC(IntPtr.Zero);
        if (screenContext == IntPtr.Zero)
        {
            throw new Win32Exception(Marshal.GetLastWin32Error(), "GetDC 失败");
        }

        IntPtr memoryContext = IntPtr.Zero;
        IntPtr bitmap = IntPtr.Zero;
        IntPtr previous = IntPtr.Zero;
        IntPtr bits = IntPtr.Zero;
        IntPtr info = IntPtr.Zero;

        try
        {
            memoryContext = NativeMethods.CreateCompatibleDC(screenContext);
            if (memoryContext == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateCompatibleDC 失败");
            }

            bitmap = NativeMethods.CreateCompatibleBitmap(screenContext, region.Width, region.Height);
            if (bitmap == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "CreateCompatibleBitmap 失败");
            }

            previous = NativeMethods.SelectObject(memoryContext, bitmap);

            if (!NativeMethods.BitBlt(
                    memoryContext, 0, 0, region.Width, region.Height,
                    screenContext, region.X, region.Y, SrcCopy))
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "BitBlt 失败");
            }

            var pixelBytes = region.Width * region.Height * 4;
            bits = Marshal.AllocHGlobal(pixelBytes);
            info = Marshal.AllocHGlobal(BitmapInfoHeaderSize);

            Marshal.WriteInt32(info, 0, BitmapInfoHeaderSize);
            Marshal.WriteInt32(info, 4, region.Width);

            // A negative height asks GDI for top-down rows, so no flip is needed.
            Marshal.WriteInt32(info, 8, -region.Height);
            Marshal.WriteInt16(info, 12, 1);
            Marshal.WriteInt16(info, 14, 32);
            Marshal.WriteInt32(info, 16, 0);
            Marshal.WriteInt32(info, 20, pixelBytes);

            var lines = NativeMethods.GetDIBits(
                memoryContext, bitmap, 0, (uint)region.Height, bits, info, DibRgbColors);
            if (lines == 0)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), "GetDIBits 失败");
            }

            var buffer = new byte[pixelBytes];
            Marshal.Copy(bits, buffer, 0, pixelBytes);
            return ToRgb(buffer, region.Width, region.Height);
        }
        finally
        {
            if (previous != IntPtr.Zero && memoryContext != IntPtr.Zero)
            {
                NativeMethods.SelectObject(memoryContext, previous);
            }

            if (bitmap != IntPtr.Zero)
            {
                NativeMethods.DeleteObject(bitmap);
            }

            if (memoryContext != IntPtr.Zero)
            {
                NativeMethods.DeleteDC(memoryContext);
            }

            NativeMethods.ReleaseDC(IntPtr.Zero, screenContext);

            if (bits != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(bits);
            }

            if (info != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(info);
            }
        }
    }

    /// <summary>BGRA to RGB, which is what the PNG encoder writes.</summary>
    private static byte[] ToRgb(byte[] bgra, int width, int height)
    {
        var pixels = width * height;
        var rgb = new byte[pixels * 3];

        for (var index = 0; index < pixels; index++)
        {
            var source = index * 4;
            var target = index * 3;
            rgb[target] = bgra[source + 2];
            rgb[target + 1] = bgra[source + 1];
            rgb[target + 2] = bgra[source];
        }

        return rgb;
    }
}
