#!/usr/bin/env pwsh
# Build the Windows installers: an MSI and a Burn-based .exe that wraps it.
#
#   ./scripts/build-installer.ps1              # publish the app, then build both
#   ./scripts/build-installer.ps1 -SkipPublish # reuse artifacts/windows/win-x64
[CmdletBinding()]
param(
    [switch]$SkipPublish,
    [string]$Version
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$appProject = 'src/apps/windows/OpenAgent.Windows/OpenAgent.Windows.csproj'
$publishDir = Join-Path $root 'artifacts/windows/win-x64'
$outDir = Join-Path $root 'artifacts/installer'
$icon = Join-Path $root 'installer/app.ico'

if (-not $Version) {
    # The SDK prints bare values for a single -getProperty and a JSON object for
    # several; accept either so the script does not depend on which one you get.
    $raw = (dotnet msbuild $appProject -getProperty:Version | Out-String).Trim()
    if ($raw.StartsWith('{')) {
        $Version = ($raw | ConvertFrom-Json).Properties.Version
    } else {
        $Version = ($raw -split "`r?`n" | Where-Object { $_ -match '^\d' } | Select-Object -First 1)
    }
    if (-not $Version) { throw 'could not read Version from the project' }
}
Write-Host "OpenAgent $Version" -ForegroundColor Cyan

if (-not (Test-Path $icon)) {
    python scripts/gen-installer-icon.py
}

if (-not $SkipPublish) {
    Write-Host 'publishing self-contained win-x64 ...'
    dotnet publish $appProject -c Release -p:Platform=x64 -r win-x64 --self-contained true -o $publishDir
    if ($LASTEXITCODE -ne 0) { throw "publish failed with exit code $LASTEXITCODE" }
}

# The published app dies at XAML init without its resource index; fail here rather
# than shipping an installer for an app that will not start.
if (-not (Test-Path (Join-Path $publishDir 'OpenAgent.pri'))) {
    throw "missing OpenAgent.pri in $publishDir - see AGENTS.md section 8.9"
}

New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$msi = Join-Path $outDir "OpenAgent-$Version-x64.msi"
$exe = Join-Path $outDir "OpenAgent-$Version-x64.exe"

Write-Host 'building MSI ...'
# -arch x64 is what makes the package's Template Summary 64-bit. Without it every
# harvested component is 32-bit and Windows Installer redirects ProgramFiles64Folder
# to Program Files (x86) — ICE80 catches it, so validate right after.
dotnet wix build installer/OpenAgent.wxs -arch x64 `
    -ext WixToolset.UI.wixext `
    -d "Version=$Version" -d "PublishDir=$publishDir" -d "Icon=$icon" `
    -o $msi
if ($LASTEXITCODE -ne 0) { throw "MSI build failed with exit code $LASTEXITCODE" }


Write-Host 'validating MSI ...'
# Blocking gate. ICE03/ICE60 are excluded on purpose: WiX's <Files> harvest never
# fills File.Language, so it is NULL for files without a version resource and an
# over-long multi-LCID list for .NET resource assemblies. That column only drives
# localisation costing during patching — it does not affect where files land or
# whether the app runs — and the harvest exposes no attribute to set it. Every
# other ICE still fails the build; ICE80 (32-bit components in a 64-bit directory)
# is exactly the class of bug this catches.
dotnet wix msi validate $msi -sice ICE03 -sice ICE60
if ($LASTEXITCODE -ne 0) { throw "MSI validation failed with exit code $LASTEXITCODE" }

# Informational: surface the suppressed findings instead of hiding them.
dotnet wix msi validate $msi 2>&1 | Select-Object -First 5

Write-Host 'building EXE bundle ...'
dotnet wix build installer/Bundle.wxs -arch x64 `
    -ext WixToolset.BootstrapperApplications.wixext `
    -d "Version=$Version" -d "MsiPath=$msi" -d "Icon=$icon" `
    -o $exe
if ($LASTEXITCODE -ne 0) { throw "bundle build failed with exit code $LASTEXITCODE" }

Get-Item $msi, $exe | ForEach-Object {
    '{0}  {1:N0} bytes' -f $_.Name, $_.Length
}
