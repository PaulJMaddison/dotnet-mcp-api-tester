# Golden path QA runbook

## Prerequisites
- Docker + Docker Compose
- .NET 8 SDK
- PowerShell (`pwsh`) for Playwright install script on Linux/macOS (or Windows PowerShell on Windows)

## One-command E2E run

Linux/macOS:

```bash
./scripts/e2e.sh
```

Windows:

```powershell
pwsh ./scripts/e2e.ps1
```

What the scripts do:
1. Create `artifacts/e2e/<timestamp>/`.
2. Start `docker-compose.e2e.yml` (SQL Server + ApiTester.Web + ApiTester.Site + FixtureApi).
3. Wait for service health/readiness endpoints.
4. Install Playwright browsers from the local project script.
5. Run `tests/ApiTester.E2E`.
6. Always capture compose logs and tear down (`docker compose down -v`).

## Manual checks (golden path)
1. **Login / access**: open `/app`, verify redirect to onboarding without OIDC and account renders at `/app/account`.
2. **Token lifecycle**: create a token in `/app/tokens`, verify API calls work, revoke token, verify API calls fail.
3. **Project workflow**: create project, import FixtureApi OpenAPI, generate plan, run tests, verify passing run appears in `/app/projects/{projectKey}/runs`.
4. **Hosted mode egress**: clear runtime policy allowlist, run plan and verify `HostedEgressDenied`, restore allowlist and rerun successfully.
5. **Evidence export**: download evidence pack zip and verify `manifest.json`, `run.json`, `policy-snapshot.json`; validate sensitive headers/tokens are redacted.
6. **Billing not configured**: call billing plan endpoint and verify `BillingNotConfigured` ProblemDetails.

## Expected outcomes
- E2E tests pass from scripts.
- On failures, artifacts include:
  - `compose.log`
  - per-test screenshot (`failure.png`)
  - per-test trace (`trace.zip`)
