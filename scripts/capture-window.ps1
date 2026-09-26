# Resize the OpenAgent window so its *client* area is exactly the artboard size,
# then render that window into a PNG.
#
# PrintWindow (with PW_RENDERFULLCONTENT) is used instead of a screen grab: the
# shell can be behind other windows, and a screen capture of the same rectangle
# silently returns whatever is on top. This way the capture is of the app itself
# and nothing on the desktop is disturbed.
param(
    [string]$ProcessName = 'OpenAgent',
    [int]$Width = 1440,
    [int]$Height = 900,
    [string]$Out = 'artifacts/shots/current.png'
)

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class Win32Cap {
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool MoveWindow(IntPtr h, int x, int y, int w, int hh, bool repaint);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr hdc, uint flags);
    [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int cmd);
    [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, IntPtr extra);
    [DllImport("user32.dll")] public static extern IntPtr SendMessageW(IntPtr h, uint msg, IntPtr w, IntPtr l);
    [DllImport("user32.dll")] public static extern void keybd_event(byte vk, byte scan, uint flags, IntPtr extra);
    [DllImport("kernel32.dll")] public static extern uint SetThreadExecutionState(uint flags);

    /// <summary>
    /// A display that has powered off stops compositing: windows keep the frame
    /// they had, brand new windows never get one, and every capture comes back a
    /// flat colour. Waking it and holding it for this process is the only way a
    /// headless run gets a real frame.
    /// </summary>
    public static void WakeDisplay() {
        SetThreadExecutionState(0x80000000u | 0x2u | 0x1u);   // continuous | display | system
        SendMessageW((IntPtr)0xffff, 0x0112, (IntPtr)0xF170, (IntPtr)1);   // SC_MONITORPOWER, on
        keybd_event(0x10, 0, 2, IntPtr.Zero);   // VK_SHIFT key-up resets the idle timer
    }
}
"@

Add-Type -AssemblyName System.Drawing

$target = Get-Process | Where-Object {
    $_.ProcessName -eq $ProcessName -and $_.MainWindowHandle -ne 0
} | Select-Object -First 1
if (-not $target) { Write-Error "no visible window for process '$ProcessName'"; exit 1 }
$hwnd = $target.MainWindowHandle

# Before anything else: a powered-off display never composites a new frame, so
# the window would be captured blank no matter how it is read.
[Win32Cap]::WakeDisplay()
Start-Sleep -Milliseconds 1200

# Each query gets its own struct instance: PowerShell wraps a value type the
# first time it is passed by reference, and a reused variable then reads back as
# zero rather than the newly written rect.
$firstWindow = New-Object Win32Cap+RECT
$firstClient = New-Object Win32Cap+RECT
$secondWindow = New-Object Win32Cap+RECT
$secondClient = New-Object Win32Cap+RECT

[void][Win32Cap]::ShowWindow($hwnd, 9)   # SW_RESTORE: a minimised window reports a 0x0 client
[void][Win32Cap]::MoveWindow($hwnd, 0, 0, $Width, $Height, $true)
Start-Sleep -Milliseconds 500

[void][Win32Cap]::GetWindowRect($hwnd, [ref]$firstWindow)
[void][Win32Cap]::GetClientRect($hwnd, [ref]$firstClient)
$cw = $firstClient.Right - $firstClient.Left
$ch = $firstClient.Bottom - $firstClient.Top

# Grow the window by exactly the non-client border so the client lands on target.
[void][Win32Cap]::MoveWindow(
    $hwnd, $firstWindow.Left, $firstWindow.Top,
    $Width + (($firstWindow.Right - $firstWindow.Left) - $cw),
    $Height + (($firstWindow.Bottom - $firstWindow.Top) - $ch), $true)
Start-Sleep -Milliseconds 900

[void][Win32Cap]::GetWindowRect($hwnd, [ref]$secondWindow)
[void][Win32Cap]::GetClientRect($hwnd, [ref]$secondClient)
$cw = $secondClient.Right - $secondClient.Left
$ch = $secondClient.Bottom - $secondClient.Top
if ($cw -ne $Width -or $ch -ne $Height) {
    Write-Error "client is ${cw}x${ch}, wanted ${Width}x${Height}"
    exit 1
}

$winW = $secondWindow.Right - $secondWindow.Left
$winH = $secondWindow.Bottom - $secondWindow.Top

# The artboards are client-area frames, so the crop has to start at the client
# origin, not the window origin: an unpackaged window still carries ~8px of
# invisible resize border, and cropping from 0,0 keeps that border (which
# PrintWindow renders black) and drops the same amount off the right and bottom.
$clientOrigin = New-Object Win32Cap+POINT
[void][Win32Cap]::ClientToScreen($hwnd, [ref]$clientOrigin)
$offsetX = $clientOrigin.X - $secondWindow.Left
$offsetY = $clientOrigin.Y - $secondWindow.Top
if ($offsetX -lt 0 -or $offsetY -lt 0 -or $offsetX + $Width -gt $winW -or $offsetY + $Height -gt $winH) {
    Write-Error "client ($offsetX,$offsetY ${Width}x${Height}) does not fit the ${winW}x${winH} window"
    exit 1
}

# A window activated with nothing focused yet hands focus to the page's first focusable
# element, and WinUI draws its adorner there: the shipped settings capture carried a 2px
# near-black rectangle around 开机启动 (x 358..1321, y 196..241) that no artboard draws,
# and it also skewed that card's measured corner. One click on empty canvas moves the
# focus to the root and the transient state is gone. (700,100) is above the first card on
# every page and clear of the composer on the agent page.
$neutral = New-Object Win32Cap+POINT
$neutral.X = $clientOrigin.X + 700
$neutral.Y = $clientOrigin.Y + 100
[void][Win32Cap]::SetCursorPos($neutral.X, $neutral.Y)
Start-Sleep -Milliseconds 200
[void][Win32Cap]::mouse_event(0x0002 -bor 0x0004, 0, 0, 0, [IntPtr]::Zero)  # left down | up
Start-Sleep -Milliseconds 400

# Park the pointer outside the window: otherwise a row under the cursor is
# captured in its hover state and the screenshot stops matching the artboard.
[void][Win32Cap]::SetCursorPos($winW + 200, $winH + 200)
Start-Sleep -Milliseconds 300

$full = New-Object System.Drawing.Bitmap($winW, $winH)
$g = [System.Drawing.Graphics]::FromImage($full)
$hdc = $g.GetHdc()
[void][Win32Cap]::PrintWindow($hwnd, $hdc, 2)   # PW_RENDERFULLCONTENT
$g.ReleaseHdc($hdc)
$g.Dispose()

$bmp = $full.Clone((New-Object System.Drawing.Rectangle(
    $offsetX, $offsetY, $Width, $Height)),
    [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$full.Dispose()

$outFull = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Out))
$dir = [System.IO.Path]::GetDirectoryName($outFull)
if (-not (Test-Path $dir)) { [void](New-Item -ItemType Directory -Force -Path $dir) }
$bmp.Save($outFull, [System.Drawing.Imaging.ImageFormat]::Png)

# A frame that is one flat colour is a capture failure, not an empty window, so
# the run still fails — but the file is written first, because a rejected frame
# is the evidence for why it was rejected. The shell is mostly flat surfaces, so
# distinct-colour counts are a poor test; the share of the single most common
# colour is not: a real frame never puts ~every sampled point on one colour.
$tally = @{}
foreach ($x in 0..19) {
    foreach ($y in 0..19) {
        $p = $bmp.GetPixel([int](($x + 1) * $Width / 21), [int](($y + 1) * $Height / 21))
        $key = "$($p.R),$($p.G),$($p.B)"
        $tally[$key] = 1 + $(if ($tally.ContainsKey($key)) { $tally[$key] } else { 0 })
    }
}
$modalShare = (($tally.Values | Measure-Object -Maximum).Maximum / 400.0)
$bmp.Dispose()

if ($modalShare -gt 0.97) {
    Write-Error ("capture is blank ({0:P1} of the sample is one colour, {1} distinct) — " +
        'the display was not compositing; see the saved frame') -f $modalShare, $tally.Count
    exit 1
}

Write-Output "client ${Width}x${Height} -> $Out (modal $($('{0:P1}' -f $modalShare)))"
