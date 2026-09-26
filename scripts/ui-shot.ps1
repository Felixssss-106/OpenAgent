# Build, run and screenshot the shell in one go, so a design pass is a single
# command. Defaults to the build output; -Publish uses the distributable folder
# instead (that is what ships, and its file set differs).
param(
    [switch]$Publish,
    [switch]$SkipBuild,
    [string]$Out = 'artifacts\shots\now.png',
    [string]$Page = '',
    [string]$Theme = '',
    [int]$Width = 1440,
    [int]$Height = 900,
    [int]$SettleMs = 5000
)

$ErrorActionPreference = 'Stop'
$root = 'D:\WorkSpace\OpenAgent'
Set-Location $root

$dir = if ($Publish) { "$root\artifacts\windows\win-x64" }
        else { "$root\src\apps\windows\OpenAgent.Windows\bin\x64\Release\net10.0-windows10.0.26100.0" }

# A running shell holds its own DLLs, so it has to go before the build.
Get-Process OpenAgent -ErrorAction SilentlyContinue | Stop-Process -Force
Start-Sleep -Milliseconds 800

if ($Publish) {
    dotnet publish "$root\src\apps\windows\OpenAgent.Windows\OpenAgent.Windows.csproj" `
        -c Release -p:Platform=x64 -r win-x64 --self-contained true -o "$root\artifacts\windows\win-x64" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "publish failed ($LASTEXITCODE)" }
} elseif (-not $SkipBuild) {
    dotnet build "$root\OpenAgent.sln" -c Release -p:Platform=x64 | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "build failed ($LASTEXITCODE)" }
}

$argList = @()
if ($Page) { $argList += "--page=$Page" }
if ($Theme) { $argList += "--theme=$Theme" }

$p = Start-Process -FilePath "$dir\OpenAgent.exe" -WorkingDirectory $dir -ArgumentList $argList -PassThru
Start-Sleep -Milliseconds $SettleMs
if ($p.HasExited) { throw "app exited immediately, code $($p.ExitCode)" }

& "$root\scripts\capture-window.ps1" -Out $Out -Width $Width -Height $Height
if (-not (Test-Path (Join-Path $root $Out))) { throw "capture produced no file" }
$p | Stop-Process -Force
Write-Output "ok $Out"
