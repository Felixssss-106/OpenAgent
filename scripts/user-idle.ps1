# Print how long since the last physical or synthetic user input, in milliseconds.
#
# The screenshot harness moves the pointer, clicks and steals foreground while it
# works, so running it under a person's hands fights them and corrupts the frames
# (a nav click in a popped-up window once got captured as the wrong page). Callers
# gate on this: below the idle threshold they must refuse to run, not wait.
#
#   powershell -File scripts/user-idle.ps1     -> e.g. 12843

Add-Type @"
using System;
using System.Runtime.InteropServices;
public static class UserIdle {
    [StructLayout(LayoutKind.Sequential)]
    public struct LASTINPUTINFO { public uint cbSize; public uint dwTime; }
    [DllImport("user32.dll")] public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);
    public static uint Milliseconds() {
        LASTINPUTINFO info = new LASTINPUTINFO();
        info.cbSize = (uint)Marshal.SizeOf(info);
        if (!GetLastInputInfo(ref info)) return 0;
        return (uint)Environment.TickCount - info.dwTime;   // uint math survives TickCount wraparound
    }
}
"@

Write-Output ([UserIdle]::Milliseconds())
