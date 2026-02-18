#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$timestamp = (Get-Date).ToUniversalTime().ToString('yyyyMMdd-HHmmss')
$outDir = Join-Path $root "artifacts/security/$timestamp"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

$resultsFile = Join-Path $outDir 'security-results.txt'
$trxFile = Join-Path $outDir 'security-tests.trx'

& {
    Write-Host "[security] started: $((Get-Date).ToUniversalTime().ToString('s'))Z"
    Write-Host '[security] project: tests/ApiTester.SecurityTests/ApiTester.SecurityTests.csproj'
    dotnet test (Join-Path $root 'tests/ApiTester.SecurityTests/ApiTester.SecurityTests.csproj') -c Release --logger 'trx;LogFileName=security-tests.trx' --results-directory $outDir
    Write-Host '[security] status: PASS'
} 2>&1 | Tee-Object -FilePath $resultsFile

Write-Host "[security] results: $resultsFile"
Write-Host "[security] trx: $trxFile"
