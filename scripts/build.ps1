#!/usr/bin/env pwsh
# Thin wrapper: build the whole solution.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet build OpenAgent.sln -c Release -p:Platform=x64
    if ($LASTEXITCODE -ne 0) { throw "build failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}
