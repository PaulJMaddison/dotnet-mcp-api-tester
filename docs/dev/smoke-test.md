# Docker compose smoke test (SQL Server)

The smoke flow validates a full local stack using SQL Server and deterministic fixture traffic.

## Prerequisites

- Docker Desktop (or Docker Engine + Compose plugin)
- `curl`, `python3`, and `unzip` for `scripts/smoke.sh`
- PowerShell 7+ for `scripts/smoke.ps1`

## Compose services

`docker-compose.yml` includes:

- `sqlserver` (`mcr.microsoft.com/mssql/server`)
- `fixture-api` (local deterministic API used by run execution)
- `apitester-web` (wired to SQL Server)
- `apitester-site` (wired to `apitester-web`)

All services include healthchecks (`/health` on web/site/fixture API + SQL query check on SQL Server).

## Environment variables

Optional variables:

- `MSSQL_SA_PASSWORD` (default: `Your_strong_Passw0rd!`)
- `SMOKE_API_KEY` (default: `dev-local-key`)
- `API_BASE_URL` (default: `http://localhost:8080`)

## Run smoke script

### Bash

```bash
./scripts/smoke.sh
```

### PowerShell

```powershell
./scripts/smoke.ps1
```

## What smoke covers

1. Starts compose stack and waits for service health.
2. Creates a project in `apitester-web`.
3. Imports `tests/fixtures/petstore-small.json`.
4. Executes run `listPets` against `fixture-api`.
5. Downloads evidence bundle zip.
6. Verifies `manifest.json` and `run.json` exist.
7. Tears down compose stack on success/failure (writes logs on failure).
