$ErrorActionPreference = 'Stop'

$root = (Resolve-Path "$PSScriptRoot/..").Path
$timestamp = Get-Date -Format 'yyyyMMdd-HHmmss'
$artifactDir = Join-Path $root "artifacts/e2e/$timestamp"
New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
$composeFile = Join-Path $root 'docker-compose.e2e.yml'

function Wait-ForServiceHealth {
    param([string]$Service, [int]$Retries = 120)
    $id = docker compose -f $composeFile ps -q $Service
    for ($i = 0; $i -lt $Retries; $i++) {
        $status = docker inspect --format "{{.State.Health.Status}}" $id 2>$null
        if ($status -eq "healthy") {
            Write-Host "$Service healthy"
            return
        }
        Start-Sleep -Seconds 2
    }
    throw "Timed out waiting for $Service health"
}

function Invoke-WaitForUrl {
    param([string]$Name, [string]$Url, [int]$Retries = 120)
    for ($i = 0; $i -lt $Retries; $i++) {
        try {
            Invoke-WebRequest -Uri $Url -UseBasicParsing | Out-Null
            Write-Host "$Name ready"
            return
        } catch {
            Start-Sleep -Seconds 2
        }
    }
    throw "Timed out waiting for $Name at $Url"
}

try {
    docker compose -f $composeFile up -d --build

    $env:E2E_BASE_URL = if ($env:E2E_BASE_URL) { $env:E2E_BASE_URL } else { 'http://localhost:18081' }
    $env:E2E_WEB_BASE_URL = if ($env:E2E_WEB_BASE_URL) { $env:E2E_WEB_BASE_URL } else { 'http://localhost:18080' }
    $env:E2E_API_KEY = if ($env:E2E_API_KEY) { $env:E2E_API_KEY } else { 'dev-local-key' }
    $env:E2E_ARTIFACTS_DIR = $artifactDir

    Wait-ForServiceHealth -Service 'sqlserver'
    $fixtureUrl = if ($env:E2E_FIXTURE_BASE_URL) { $env:E2E_FIXTURE_BASE_URL } else { 'http://localhost:18082' }
    Invoke-WaitForUrl -Name 'fixtureapi' -Url "$fixtureUrl/health"
    Invoke-WaitForUrl -Name 'web' -Url "$($env:E2E_WEB_BASE_URL)/health"
    Invoke-WaitForUrl -Name 'site' -Url "$($env:E2E_BASE_URL)/health"

    dotnet build "$root/tests/ApiTester.E2E/ApiTester.E2E.csproj" -c Release | Out-Null
    pwsh "$root/tests/ApiTester.E2E/bin/Release/net8.0/playwright.ps1" install --with-deps chromium
    dotnet test "$root/tests/ApiTester.E2E/ApiTester.E2E.csproj" -c Release --filter "Category=E2E"
}
finally {
    docker compose -f $composeFile logs | Out-File -FilePath (Join-Path $artifactDir 'compose.log') -Encoding utf8
    docker compose -f $composeFile down -v
}
