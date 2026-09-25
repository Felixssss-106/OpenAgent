#!/usr/bin/env pwsh
# Thin wrapper: run the whole test suite.
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
Push-Location $root
try {
    dotnet test OpenAgent.sln -c Release --logger "console;verbosity=normal"
    if ($LASTEXITCODE -ne 0) { throw "tests failed with exit code $LASTEXITCODE" }
}
finally {
    Pop-Location
}
