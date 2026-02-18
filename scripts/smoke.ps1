$ErrorActionPreference = 'Stop'

$RootDir = Resolve-Path (Join-Path $PSScriptRoot '..')
$ComposeFile = if ($env:COMPOSE_FILE) { $env:COMPOSE_FILE } else { Join-Path $RootDir 'docker-compose.yml' }
$ApiBaseUrl = if ($env:API_BASE_URL) { $env:API_BASE_URL } else { 'http://localhost:8080' }
$SmokeApiKey = if ($env:SMOKE_API_KEY) { $env:SMOKE_API_KEY } else { 'dev-local-key' }
$ArtifactDir = Join-Path $RootDir 'artifacts/smoke'
$EvidenceZip = Join-Path $ArtifactDir 'evidence.zip'
$LogFile = Join-Path $ArtifactDir 'compose.log'

New-Item -ItemType Directory -Force -Path $ArtifactDir | Out-Null

function Wait-ServiceHealth([string]$ContainerName) {
    $timeout = [TimeSpan]::FromMinutes(3)
    $start = Get-Date

    while ((Get-Date) - $start -lt $timeout) {
        $status = docker inspect --format '{{if .State.Health}}{{.State.Health.Status}}{{else}}{{.State.Status}}{{end}}' $ContainerName 2>$null
        if ($status -eq 'healthy' -or $status -eq 'running') {
            return
        }

        Start-Sleep -Seconds 3
    }

    throw "Timed out waiting for $ContainerName health."
}

try {
    docker compose -f $ComposeFile up -d --build

    Wait-ServiceHealth 'apitester-sqlserver'
    Wait-ServiceHealth 'apitester-fixture-api'
    Wait-ServiceHealth 'apitester-web'
    Wait-ServiceHealth 'apitester-site'

    $headers = @{ 'X-Api-Key' = $SmokeApiKey }

    $project = Invoke-RestMethod -Method Post -Uri "$ApiBaseUrl/api/projects" -Headers $headers -ContentType 'application/json' -Body '{"name":"smoke-project"}'
    $projectId = $project.projectId

    $fixturePath = Join-Path $RootDir 'tests/fixtures/petstore-small.json'
    curl.exe -fsS -H "X-Api-Key: $SmokeApiKey" -F "file=@$fixturePath;type=application/json" "$ApiBaseUrl/api/projects/$projectId/openapi/import" | Out-Null

    $run = Invoke-RestMethod -Method Post -Uri "$ApiBaseUrl/api/projects/$projectId/runs/execute/listPets" -Headers $headers
    $runId = $run.runId

    curl.exe -fsS -H "X-Api-Key: $SmokeApiKey" "$ApiBaseUrl/runs/$runId/export/evidence-bundle" -o $EvidenceZip

    $entries = & tar -tf $EvidenceZip
    if (-not ($entries -match 'manifest.json')) {
        throw 'manifest.json missing from evidence bundle.'
    }

    if (-not ($entries -match 'run.json')) {
        throw 'run.json missing from evidence bundle.'
    }

    Write-Host "Smoke test passed. Evidence bundle saved at $EvidenceZip"
}
catch {
    docker compose -f $ComposeFile logs --no-color | Out-File -Encoding utf8 -FilePath $LogFile
    throw
}
finally {
    docker compose -f $ComposeFile down -v --remove-orphans
}
