#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$resultsDir = Join-Path $root 'artifacts/test-results'
$logsDir = Join-Path $resultsDir 'logs'
New-Item -ItemType Directory -Path $resultsDir -Force | Out-Null
New-Item -ItemType Directory -Path $logsDir -Force | Out-Null

function Write-Log([string]$Message) {
    Write-Host "[green] $Message"
}

function Show-FailureSummary {
    $trxFiles = Get-ChildItem -Path $resultsDir -Filter '*.trx' -ErrorAction SilentlyContinue
    if (-not $trxFiles) {
        return
    }

    Write-Log 'Failing tests summary:'
    foreach ($trx in $trxFiles) {
        [xml]$xml = Get-Content -Path $trx.FullName
        $namespace = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
        $namespace.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
        $failed = $xml.SelectNodes('//t:UnitTestResult[@outcome="Failed"]', $namespace)
        if ($failed.Count -eq 0) { continue }

        Write-Host "- $($trx.Name)"
        foreach ($item in $failed | Select-Object -First 10) {
            Write-Host "  * $($item.testName)"
        }
    }
}

function Run-Step([string]$Name, [string]$Command) {
    Write-Log $Name
    $safeName = $Name.Replace(' ', '_')
    $logFile = Join-Path $logsDir "$safeName.log"

    & pwsh -NoProfile -Command $Command *>&1 | Tee-Object -FilePath $logFile
    if ($LASTEXITCODE -ne 0) {
        Write-Log "FAILED: $Name (exit code $LASTEXITCODE)"
        Write-Log "Artifacts: $resultsDir"
        Show-FailureSummary
        exit $LASTEXITCODE
    }
}

Write-Log "OS: $([System.Runtime.InteropServices.RuntimeInformation]::OSDescription)"
Write-Log 'dotnet info:'
dotnet --info

Run-Step 'dotnet restore' 'dotnet restore'
Run-Step 'dotnet build release' 'dotnet build -c Release'
Run-Step 'dotnet test release' "dotnet test -c Release --logger 'trx;LogFilePrefix=test_results' --results-directory '$resultsDir'"

$trxFiles = Get-ChildItem -Path $resultsDir -Filter '*.trx' -ErrorAction SilentlyContinue
$assemblyCount = ($trxFiles | Measure-Object).Count
$passed = 0
$failed = 0
$skipped = 0

foreach ($trx in $trxFiles) {
    [xml]$xml = Get-Content -Path $trx.FullName
    $namespace = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $namespace.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $counters = $xml.SelectSingleNode('//t:Counters', $namespace)
    if ($null -eq $counters) { continue }

    $passed += [int]$counters.passed
    $failed += [int]$counters.failed
    $skipped += [int]$counters.notExecuted
}

Write-Log 'build success'
Write-Log "test assemblies executed: $assemblyCount"
Write-Log "test summary: passed=$passed failed=$failed skipped=$skipped total=$($passed + $failed + $skipped)"
Write-Log "artifacts: $resultsDir"
