# GitHub Actions CLI smoke run

Use `.github/workflows/example-smoke.yml` as a minimal template for headless API test execution in CI.

## Required repository secrets

- `APITESTER_BASE_URL`: Base URL for ApiTester (example: `https://apitester.example.com`)
- `APITESTER_TOKEN`: Bearer token with `projects:read`, `runs:read`, and `runs:write`
- `APITESTER_PROJECT_ID`: Project GUID
- `APITESTER_OPERATION_ID`: OpenAPI operation id to execute

## What the sample workflow does

1. Checks out source.
2. Installs .NET 8 SDK.
3. Restores dependencies.
4. Executes:

```bash
dotnet run --project ApiTester.Cli -- run execute --project "$APITESTER_PROJECT_ID" --operation "$APITESTER_OPERATION_ID"
```

The CLI reads `APITESTER_BASE_URL` and `APITESTER_TOKEN` from environment variables, sends Bearer auth, and avoids printing token values.
