param(
    [switch]$Docker,
    [switch]$Smoke
)

$ErrorActionPreference = 'Stop'
$RootDir = Resolve-Path (Join-Path $PSScriptRoot '..')

if ($Smoke) {
    $Docker = $true
}

Push-Location $RootDir
try {
    dotnet restore
    dotnet build -c Release --no-restore
    dotnet test -c Release --no-build --logger trx

    if ($Docker) {
        docker build -f ApiTester.Web/Dockerfile -t apitester-web:local .
        docker build -f ApiTester.Site/Dockerfile -t apitester-site:local .
    }

    if ($Smoke) {
        & (Join-Path $RootDir 'scripts/smoke.ps1')
    }
}
finally {
    Pop-Location
}
