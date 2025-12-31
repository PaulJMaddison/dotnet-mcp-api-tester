# Dotnet MCP API Tester

## Local development

```bash
dotnet restore
dotnet build -c Release
dotnet test -c Release
```

## Local run

One command to start the Web API + UI:

```bash
./scripts/local-run.sh
```

Run them separately (two terminals) if you prefer:

```bash
dotnet run --project ApiTester.Web --launch-profile "ApiTester.Web"
dotnet run --project ApiTester.Ui --launch-profile "ApiTester.Ui"
```

To use SQL Server persistence in Development, set a connection string (for example, `Persistence__ConnectionString`).

## Smoke test

With the Web API + UI running:

```bash
./scripts/smoke-test.sh
```
