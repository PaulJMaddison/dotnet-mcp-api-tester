# Build and test (Release)

Use the root build scripts to run the same Release-oriented flow locally that CI runs:

## Linux/macOS
# Local build and test (CI-equivalent)

The repository includes local scripts that mirror the release CI steps in `Release` mode.

## Prerequisites

- .NET 8 SDK
- Docker Desktop (or Docker Engine + Compose plugin) if you use Docker/smoke flags

## Bash

```bash
./scripts/build.sh
```

## Windows (PowerShell)
Optional flags:

```bash
./scripts/build.sh --docker
./scripts/build.sh --smoke
```

What the script runs:

1. `dotnet restore`
2. `dotnet build -c Release --no-restore`
3. `dotnet test -c Release --no-build --logger trx`
4. Optional: `docker build` for `ApiTester.Web` and `ApiTester.Site`
5. Optional: smoke flow via `scripts/smoke.sh`

## PowerShell

```powershell
./scripts/build.ps1
```

Both scripts run, in order:

1. `dotnet restore DotnetMcpApiTester.sln`
2. `dotnet build DotnetMcpApiTester.sln -c Release --no-restore`
3. `dotnet test DotnetMcpApiTester.sln -c Release --no-build`
4. `dotnet publish ApiTester.Web/ApiTester.Web.csproj -c Release --no-build -o <temp>/web`
5. `dotnet publish ApiTester.Site/ApiTester.Site.csproj -c Release --no-build -o <temp>/site`

If any step fails, the script exits with a non-zero code.

## Manual commands

If you want to run each step directly:

```bash
dotnet restore DotnetMcpApiTester.sln
dotnet build DotnetMcpApiTester.sln -c Release --no-restore
dotnet test DotnetMcpApiTester.sln -c Release --no-build
```
Optional switches:

```powershell
./scripts/build.ps1 -Docker
./scripts/build.ps1 -Smoke
```

## Manual GitHub Actions workflow

The CI workflow is **manual-only** to avoid automatic compute cost:

- Workflow: `.github/workflows/ci.yml`
- Trigger: `workflow_dispatch`

To run it:

1. Open Actions tab in GitHub.
2. Select **CI**.
3. Click **Run workflow**.

It restores/builds/tests in `Release`, uploads TRX files, and builds `apitester-web` + `apitester-site` Docker images.
