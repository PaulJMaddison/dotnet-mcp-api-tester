#!/usr/bin/env pwsh
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$RootDir = Resolve-Path (Join-Path $PSScriptRoot '..')
$SolutionPath = Join-Path $RootDir 'DotnetMcpApiTester.sln'
$ArtifactRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("apitester-build-{0}" -f ([System.Guid]::NewGuid().ToString('N')))
$WebPublishDir = Join-Path $ArtifactRoot 'web'
$SitePublishDir = Join-Path $ArtifactRoot 'site'

New-Item -ItemType Directory -Path $WebPublishDir -Force | Out-Null
New-Item -ItemType Directory -Path $SitePublishDir -Force | Out-Null

Push-Location $RootDir
try {
    Write-Host '[build] restore (Release pipeline)'
    dotnet restore $SolutionPath
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host '[build] build (Release)'
    dotnet build $SolutionPath -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host '[build] test (Release)'
    dotnet test $SolutionPath -c Release --no-build
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "[build] publish ApiTester.Web -> $WebPublishDir"
    dotnet publish (Join-Path $RootDir 'ApiTester.Web/ApiTester.Web.csproj') -c Release --no-build -o $WebPublishDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "[build] publish ApiTester.Site -> $SitePublishDir"
    dotnet publish (Join-Path $RootDir 'ApiTester.Site/ApiTester.Site.csproj') -c Release --no-build -o $SitePublishDir
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

    Write-Host "[build] complete. Artifacts at $ArtifactRoot"
}
finally {
    Pop-Location
}
