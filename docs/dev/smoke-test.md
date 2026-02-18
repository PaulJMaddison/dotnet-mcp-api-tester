# Smoke test

The smoke path validates the core end-to-end flow with local dependencies only:

- create/identify two tenants
- mint API token
- create project
- import a small OpenAPI fixture
- generate and execute a plan against a local fixture API
- verify run persistence
- download evidence bundle and validate `manifest.json` + `run.json`

This path is covered by `SmokeFlowTests` in `ApiTester.Web.IntegrationTests`.

## Run smoke test in Release

```bash
dotnet test ApiTester.Web.IntegrationTests/ApiTester.Web.IntegrationTests.csproj -c Release --filter "FullyQualifiedName~SmokeFlowTests"
```

## Docker compose quick checks

From repository root:

```bash
docker compose up --build -d
curl -fsS http://localhost:5000/health
curl -fsS http://localhost:8080/health
```

If your compose file maps different ports, adjust URLs accordingly.

Bring stack down:

```bash
docker compose down
```
