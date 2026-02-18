#!/usr/bin/env pwsh
param(
    [switch]$Docker
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RootDir = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $RootDir

function Invoke-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Label,
        [Parameter(Mandatory = $true)][scriptblock]$Action
    )

    Write-Host "[build] $Label"
    & $Action
    $exitCode = $LASTEXITCODE
    if ($exitCode -ne 0) {
        Write-Host "[build][WARN] '$Label' failed with exit code $exitCode"
    }

    return $exitCode
}

function Invoke-QualityGate {
    Write-Host '[gate] scanning for commercial-release markers in production code'
    $warnings = $false

    $prodPaths = @('ApiTester.Web', 'ApiTester.Site', 'ApiTester.Ui', 'ApiTester.McpServer', 'ApiTester.Cli', 'ApiTester.AI', 'ApiTester.Rag')
    $uiPaths = @('ApiTester.Site/Components/Pages', 'ApiTester.Ui/Pages')
    $patterns = @('NotImplementedException', 'TODO:', 'placeholder', 'stub', 'hack', 'temp')

    foreach ($pattern in $patterns) {
        $targetPaths = $prodPaths
        if ($pattern -eq 'placeholder') {
            $targetPaths = $uiPaths
        }

        rg -n -i $pattern $targetPaths '--glob' '!**/bin/**' '--glob' '!**/obj/**' '--glob' '!**/Test*/**' '--glob' '!**/*.md'
        if ($LASTEXITCODE -eq 0) {
            Write-Host "[gate][WARN] Found '$pattern' marker in production paths (non-blocking)."
            $warnings = $true
        }
    }

    if ($warnings) {
        Write-Host '[gate][WARN] quality gate completed with warnings'
    }
    else {
        Write-Host '[gate] quality gate passed'
    }
}

try {
    $restoreCode = Invoke-Step -Label 'dotnet restore' -Action { dotnet restore }
    if ($restoreCode -ne 0) {
        Write-Host '[build][WARN] restore failed; possible proxy/NU1301/403 environment issue. Continuing without restore retry.'
    }

    $buildCode = Invoke-Step -Label 'dotnet build -c Release --no-restore' -Action { dotnet build -c Release --no-restore }
    $testCode = Invoke-Step -Label 'dotnet test -c Release --no-build --no-restore --logger trx' -Action { dotnet test -c Release --no-build --no-restore --logger trx }

    Invoke-QualityGate

    if ($Docker) {
        [void](Invoke-Step -Label 'docker build (web/site/smokeapi-if-present)' -Action { docker build -f ApiTester.Web/Dockerfile -t apitester-web:local . })
        [void](Invoke-Step -Label 'docker build (site)' -Action { docker build -f ApiTester.Site/Dockerfile -t apitester-site:local . })
        if (Test-Path 'ApiTester.SmokeApi/Dockerfile') {
            [void](Invoke-Step -Label 'docker build (smokeapi)' -Action { docker build -f ApiTester.SmokeApi/Dockerfile -t apitester-smokeapi:local . })
        }
    }

    if ($buildCode -ne 0 -or $testCode -ne 0) {
        exit 1
    }
}
finally {
    Pop-Location
}
