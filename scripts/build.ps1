#!/usr/bin/env pwsh
param(
    [switch]$Docker
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RootDir = Resolve-Path (Join-Path $PSScriptRoot '..')
Push-Location $RootDir

function Invoke-QualityGate {
    Write-Host '[gate] scanning for banned placeholders/stubs in production code'
    $failed = $false

    $prodPaths = @('ApiTester.Web','ApiTester.Site','ApiTester.Ui','ApiTester.McpServer','ApiTester.Cli','ApiTester.AI','ApiTester.Rag')

    if (rg -n 'NotImplementedException' $prodPaths '--glob' '!**/bin/**' '--glob' '!**/obj/**') { $failed = $true; Write-Host '[gate] Found NotImplementedException in production paths.' }
    if (rg -n 'TODO:' $prodPaths '--glob' '!**/bin/**' '--glob' '!**/obj/**') { $failed = $true; Write-Host '[gate] Found TODO: marker in production paths.' }

    $uiPages = @('ApiTester.Site/Components/Pages','ApiTester.Ui/Pages')
    if (rg -n 'placeholder' $uiPages '--glob' '!**/bin/**' '--glob' '!**/obj/**') { $failed = $true; Write-Host '[gate] Found placeholder marker in production UI routes/pages.' }

    if ($failed) {
        throw '[gate] quality gate failed'
    }

    Write-Host '[gate] quality gate passed'
}

try {
    Write-Host '[build] dotnet restore'
    dotnet restore

    Write-Host '[build] dotnet build -c Release'
    dotnet build -c Release --no-restore

    Write-Host '[build] dotnet test -c Release --logger trx'
    dotnet test -c Release --no-build --logger trx

    Invoke-QualityGate

    if ($Docker) {
        Write-Host '[build] docker build (web/site/smokeapi-if-present)'
        docker build -f ApiTester.Web/Dockerfile -t apitester-web:local .
        docker build -f ApiTester.Site/Dockerfile -t apitester-site:local .
        if (Test-Path 'ApiTester.SmokeApi/Dockerfile') {
            docker build -f ApiTester.SmokeApi/Dockerfile -t apitester-smokeapi:local .
        }
    }
}
finally {
    Pop-Location
}
