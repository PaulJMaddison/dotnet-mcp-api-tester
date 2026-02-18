# Build and test (Release)

Use the root build scripts to run the same Release-oriented flow locally that CI runs:

## Linux/macOS

```bash
./scripts/build.sh
```

## Windows (PowerShell)

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
