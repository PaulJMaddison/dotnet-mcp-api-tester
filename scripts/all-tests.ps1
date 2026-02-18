#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$timestamp = Get-Date -AsUTC -Format 'yyyyMMdd-HHmmss'
$artifactRoot = Join-Path $root "artifacts/all-tests/$timestamp"
$trxDir = Join-Path $artifactRoot 'trx'
$logDir = Join-Path $artifactRoot 'logs'
$summaryFile = Join-Path $artifactRoot 'summary.txt'

New-Item -ItemType Directory -Path $trxDir -Force | Out-Null
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

$buildStatus = 'NOT_RUN'
$dotnetTestStatus = 'NOT_RUN'
$smokeStatus = 'NOT_RUN'
$e2eStatus = 'NOT_RUN'
$securityStatus = 'NOT_RUN'
$perfStatus = 'NOT_RUN'
$dotnetTrxPath = Join-Path $trxDir 'dotnet-tests.trx'

function Write-Log([string]$Message) {
    Write-Host "[all-tests] $Message"
}

function Write-Summary {
    $lines = @(
        "build status: $buildStatus"
        "dotnet test status: $dotnetTestStatus"
        "dotnet trx path: $dotnetTrxPath"
        "smoke status: $smokeStatus"
        "e2e status: $e2eStatus"
        "security status: $securityStatus"
        "perf sanity status: $perfStatus"
        "artifact folder path: $artifactRoot"
    )

    $lines | Tee-Object -FilePath $summaryFile
}

function Run-Step([string]$Name, [scriptblock]$Action) {
    Write-Log $Name
    $logFile = Join-Path $logDir ("{0}.log" -f ($Name -replace ' ', '_'))

    & {
        & $Action
    } *>&1 | Tee-Object -FilePath $logFile

    if ($LASTEXITCODE -ne 0) {
        throw "Step '$Name' failed with exit code $LASTEXITCODE"
    }
}

try {
    Run-Step 'dotnet restore' { dotnet restore }
    Run-Step 'dotnet build release' { dotnet build -c Release }
    $buildStatus = 'PASS'

    Run-Step 'dotnet test release' { dotnet test -c Release --logger "trx;LogFileName=dotnet-tests.trx" --results-directory $trxDir }
    $dotnetTestStatus = 'PASS'

    $smokeScript = Join-Path $root 'scripts/smoke.ps1'
    if (Test-Path $smokeScript) {
        Run-Step 'smoke suite' { & $smokeScript }
        $smokeStatus = 'PASS'
        $smokeComposeLog = Join-Path $root 'artifacts/smoke/compose.log'
        if (Test-Path $smokeComposeLog) {
            Copy-Item -Path $smokeComposeLog -Destination (Join-Path $artifactRoot 'smoke-compose.log') -Force
        }
    }
    else {
        $smokeStatus = 'SKIPPED (no smoke script found)'
    }

    $e2eScript = Join-Path $root 'scripts/e2e.ps1'
    if (Test-Path $e2eScript) {
        Run-Step 'e2e suite' { & $e2eScript }
        $e2eStatus = 'PASS'
        $e2eArtifacts = Join-Path $root 'artifacts/e2e'
        if (Test-Path $e2eArtifacts) {
            Copy-Item -Path $e2eArtifacts -Destination (Join-Path $artifactRoot 'e2e') -Recurse -Force
        }
    }
    else {
        $e2eStatus = 'SKIPPED (no e2e script found)'
    }

    $securityScript = Join-Path $root 'scripts/security.ps1'
    $securityProject = Join-Path $root 'tests/ApiTester.SecurityTests/ApiTester.SecurityTests.csproj'
    if (Test-Path $securityScript) {
        Run-Step 'security suite' { & $securityScript }
        $securityStatus = 'PASS'
        $securityArtifacts = Join-Path $root 'artifacts/security'
        if (Test-Path $securityArtifacts) {
            Copy-Item -Path $securityArtifacts -Destination (Join-Path $artifactRoot 'security') -Recurse -Force
        }
    }
    elseif (Test-Path $securityProject) {
        Run-Step 'security suite direct' { dotnet test $securityProject -c Release --logger "trx;LogFileName=security-tests.trx" --results-directory (Join-Path $artifactRoot 'security') }
        $securityStatus = 'PASS'
    }
    else {
        $securityStatus = 'SKIPPED (no security suite found)'
    }

    $perfFiles = Get-ChildItem -Path $root -Recurse -File -Include '*k6*.js','k6*.js','*load*.js' | Where-Object { $_.FullName -notmatch '[\\/](bin|obj)[\\/]' }
    if ($perfFiles.Count -gt 0) {
        if (Get-Command k6 -ErrorAction SilentlyContinue) {
            foreach ($perfFile in $perfFiles) {
                Run-Step ("perf sanity {0}" -f $perfFile.Name) { k6 run --duration 30s --vus 1 $perfFile.FullName }
            }
            $perfStatus = 'PASS'
        }
        else {
            $perfStatus = 'WARNING (k6 missing; perf scripts present but not documented as required)'
            Write-Log $perfStatus
        }
    }
    else {
        $perfStatus = 'PASS (no perf harness scripts found)'
    }

    Write-Summary
    Write-Log 'ALL SUITES PASSED'
}
catch {
    Write-Log "FAILED: $($_.Exception.Message)"
    Write-Summary
    exit 1
}
