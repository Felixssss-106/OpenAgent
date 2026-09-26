# Types a prompt into the focused OpenAgent window and sends it, then leaves the window
# alone so scripts/capture-window.ps1 can shoot it.
#
# The text arrives as a comma-separated list of Unicode code points rather than literal
# characters: a .ps1 saved without a BOM is read as ANSI by Windows PowerShell 5.1, which
# silently turns CJK into mojibake, and Git Bash heredocs mangle it before that.
#
#   powershell -File scripts/send-prompt.ps1 -CodePoints 25171,24320 -DelaySeconds 8

param(
    [Parameter(Mandatory = $true)][string]$CodePoints,
    [int]$DelaySeconds = 8,
    [string]$ProcessName = 'OpenAgent'
)

# -File passes every argument as one string, so a comma list cannot be declared [int[]]:
# PowerShell tries to cast the whole string to a single Int32 and fails.
$points = @($CodePoints.Split(',') | ForEach-Object { [int]$_.Trim() })

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName Microsoft.VisualBasic

$sig = @'
[DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
[DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
[DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
'@
Add-Type -MemberDefinition $sig -Name Native -Namespace OaWin

$proc = Get-Process -Name $ProcessName -ErrorAction SilentlyContinue |
    Where-Object { $_.MainWindowHandle -ne [IntPtr]::Zero } |
    Select-Object -First 1

if (-not $proc) {
    Write-Error "no $ProcessName window to type into"
    exit 1
}

# Windows refuses SetForegroundWindow to a process that does not already own the
# foreground, and a freshly killed sibling is often enough to lose that right. Tapping
# ALT is the documented way to convince the shell that the caller is receiving input;
# AppActivate goes through a different code path and sometimes lands when the raw call
# does not. Retry because both are best-effort.
$VK_MENU = 0x12
$KEYEVENTF_KEYUP = 2
$activated = $false

for ($attempt = 1; $attempt -le 6 -and -not $activated; $attempt++) {
    [void][OaWin.Native]::keybd_event([byte]$VK_MENU, 0, [uint32]$KEYEVENTF_KEYUP, [UIntPtr]::Zero)
    [void][OaWin.Native]::SetForegroundWindow($proc.MainWindowHandle)
    Start-Sleep -Milliseconds 400
    if ([OaWin.Native]::GetForegroundWindow() -eq $proc.MainWindowHandle) {
        $activated = $true
        break
    }
    try { [Microsoft.VisualBasic.Interaction]::AppActivate($proc.Id) } catch { }
    Start-Sleep -Milliseconds 300
    if ([OaWin.Native]::GetForegroundWindow() -eq $proc.MainWindowHandle) {
        $activated = $true
        break
    }
}

if (-not $activated) {
    Write-Error "could not take foreground after 6 attempts (got $([OaWin.Native]::GetForegroundWindow()), wanted $($proc.MainWindowHandle))"
    exit 1
}

$text = -join ($points | ForEach-Object { [char]$_ })
[System.Windows.Forms.SendKeys]::SendWait($text)
Start-Sleep -Milliseconds 400
[System.Windows.Forms.SendKeys]::SendWait("{ENTER}")

Start-Sleep -Seconds $DelaySeconds
Write-Host "sent $($text.Length) chars to pid $($proc.Id)"
