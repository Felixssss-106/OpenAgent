using System.Runtime.InteropServices;

namespace OpenAgent.Tools.Screen;

/// <summary>
/// GDI / User32 entry points used by the screenshot tool.
///
/// Why P/Invoke and not WinRT: <c>OpenAgent.Tools</c> targets
/// <c>net10.0</c> (no <c>-windows</c> TFM), so WinUI / WinRT interop is not
/// available here. GDI is the most portable way to grab pixels on Windows, it
/// needs no package reference, and it keeps the tool in the tool layer instead
/// of leaking it into the UI project (spec section 132). A
/// <c>net10.0-windows10.0.26100</c> project would allow Direct3D / WinRT
/// capture later; that is the upgrade path, not a reason to stub this tool.
///
/// Plain <c>DllImport</c> is used on purpose: the source-generated
/// <c>LibraryImport</c> variant would force <c>AllowUnsafeBlocks</c> on the
/// whole tool assembly, and nothing here needs marshalling (every argument is
/// blittable).
/// </summary>
internal static class NativeMethods
{
    private const string User32 = "user32.dll";

    private const string Gdi32 = "gdi32.dll";

    internal const int SmXVirtualScreen = 76;

    internal const int SmYVirtualScreen = 77;

    internal const int SmCxVirtualScreen = 78;

    internal const int SmCyVirtualScreen = 79;

    internal const int SmCxScreen = 0;

    internal const int SmCyScreen = 1;

    [DllImport(User32)]
    internal static extern IntPtr GetDC(IntPtr window);

    [DllImport(User32)]
    internal static extern int ReleaseDC(IntPtr window, IntPtr deviceContext);

    [DllImport(User32)]
    internal static extern int GetSystemMetrics(int index);

    [DllImport(User32)]
    internal static extern IntPtr GetForegroundWindow();

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr window, IntPtr rect);

    [DllImport(Gdi32)]
    internal static extern IntPtr CreateCompatibleDC(IntPtr deviceContext);

    [DllImport(Gdi32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteDC(IntPtr deviceContext);

    [DllImport(Gdi32)]
    internal static extern IntPtr CreateCompatibleBitmap(IntPtr deviceContext, int width, int height);

    [DllImport(Gdi32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(IntPtr handle);

    [DllImport(Gdi32)]
    internal static extern IntPtr SelectObject(IntPtr deviceContext, IntPtr handle);

    [DllImport(Gdi32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool BitBlt(
        IntPtr destinationContext,
        int xDestination,
        int yDestination,
        int width,
        int height,
        IntPtr sourceContext,
        int xSource,
        int ySource,
        uint rasterOperation);

    [DllImport(Gdi32, SetLastError = true)]
    internal static extern int GetDIBits(
        IntPtr deviceContext,
        IntPtr bitmap,
        uint startScan,
        uint scanLines,
        IntPtr bits,
        IntPtr info,
        uint usage);
}
