# Deployment notes

This document covers shipping ApiTester as a production SaaS.

## 1) Configuration templates (no secrets)

Use these templates as your starting point and inject real values through environment variables or secret stores at deploy time:

- `ApiTester.Web/appsettings.Template.json`
- `ApiTester.Web/appsettings.Production.json`
- `ApiTester.Ui/appsettings.Template.json`
- `ApiTester.McpServer/appsettings.Template.json`

Do not commit real API keys, database passwords, or provider tokens.

## 2) Container deployment

### Build image

```bash
docker build -t apitester-web:latest -f ApiTester.McpServer/Dockerfile .
```

### Run image

```bash
docker run --rm -p 8080:8080   -e ASPNETCORE_URLS=http://+:8080   -e Persistence__Provider=SqlServer   -e Persistence__ConnectionString="<connection-string>"   -e Auth__ApiKeys__0="<production-api-key>"   -e AI__OpenAI__ApiKey="<openai-key>"   apitester-web:latest
```

For Kubernetes or container apps, map the same keys in your secret manager and config map.

## 3) IIS / Kestrel deployment

### Kestrel (systemd or service manager)

1. Publish the API:

```bash
dotnet publish ApiTester.Web/ApiTester.Web.csproj -c Release -o ./publish/web
```

2. Set production environment variables (`ASPNETCORE_ENVIRONMENT=Production`) and required secrets.
3. Run behind Nginx/Apache/ALB with TLS termination.

### IIS (Windows)

1. Install ASP.NET Core Hosting Bundle.
2. Publish app output from `dotnet publish`.
3. Create IIS site and app pool (`No Managed Code`).
4. Set environment variables in `web.config` or IIS configuration.
5. Ensure `Auth__ApiKeys__*`, `Persistence__ConnectionString`, and `AI__OpenAI__ApiKey` come from secure configuration.

## 4) Database setup and migrations script usage

The repository provides an idempotent SQL script flow in `db/README.md` and `db/schema.sql`.

Typical release flow:

1. Generate/update the idempotent script (when migrations change):

```bash
dotnet ef migrations script --idempotent   --project ApiTester.McpServer/ApiTester.McpServer.csproj   --startup-project ApiTester.Web/ApiTester.Web.csproj   --output db/schema.sql
```

2. Apply `db/schema.sql` to the target SQL Server environment with your release tooling.
3. Verify `__EFMigrationsHistory` updated and app starts cleanly.

## 5) Production logging safety

The API logs request method, path, status code, duration, and correlation ID. It intentionally avoids logging request bodies and redacts API keys via middleware before authentication.

Production recommendations:

- Keep `Logging:LogLevel:Default` at `Warning` in production.
- Do not enable body logging in reverse proxies.
- Route logs to centralized storage with retention and access controls.
- Treat user-supplied headers and query values as potentially sensitive.

## 6) Security operations references

For security operations and regression guidance, use:

- `docs/security/README.md`
- `docs/security/threat-model.md`
- `SECURITY.md`

Run security regression checks locally before release promotion:

```bash
./scripts/security.sh
```

```powershell
pwsh ./scripts/security.ps1
```

