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
}
"@

Add-Type -AssemblyName System.Drawing

$target = Get-Process | Where-Object {
    $_.ProcessName -eq $ProcessName -and $_.MainWindowHandle -ne 0
} | Select-Object -First 1
if (-not $target) { Write-Error "no visible window for process '$ProcessName'"; exit 1 }
$hwnd = $target.MainWindowHandle

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
    $secondClient.Left, $secondClient.Top, $Width, $Height)),
    [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$full.Dispose()

$outFull = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $Out))
$dir = [System.IO.Path]::GetDirectoryName($outFull)
if (-not (Test-Path $dir)) { [void](New-Item -ItemType Directory -Force -Path $dir) }
$bmp.Save($outFull, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

Write-Output "client ${Width}x${Height} -> $Out"
