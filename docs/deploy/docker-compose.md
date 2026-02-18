# Docker Compose deployment (Web + Site + PostgreSQL)

This stack runs three services:

- `db`: PostgreSQL 16
- `web`: ApiTester API (`ApiTester.Web`)
- `site`: ApiTester UI/site (`ApiTester.Site`)

`docker-compose.yml` is configured to run with open-source base/runtime images only:

- `postgres:16-alpine`
- `mcr.microsoft.com/dotnet/sdk:8.0`
- `mcr.microsoft.com/dotnet/aspnet:8.0`

## Prerequisites

- Docker Engine 24+ with Compose V2 (`docker compose` command)

## Start the stack

From repository root:

```bash
docker compose build
docker compose up -d
```

## Verify health

```bash
docker compose ps
docker compose logs -f web
docker compose logs -f site
```

Health endpoints:

- Web: `http://localhost:8080/health`
- Site: `http://localhost:8081/health`

## Stop the stack

```bash
docker compose down
```

To remove volumes too:

```bash
docker compose down -v
```

## Environment variables used by the compose stack

### `db` service

- `POSTGRES_DB` (default in compose: `apitester`): PostgreSQL database name.
- `POSTGRES_USER` (default: `apitester`): PostgreSQL login user.
- `POSTGRES_PASSWORD` (default: `apitester`): PostgreSQL login password.

### `web` service

- `ASPNETCORE_ENVIRONMENT` (default: `Production`): ASP.NET Core environment.
- `ASPNETCORE_URLS` (default: `http://+:8080`): bind address/port in container.
- `Persistence__Provider` (default: `PostgreSql`): persistence provider selector (`File`, `SqlServer`, `PostgreSql`).
- `Persistence__ConnectionString`: ADO.NET connection string for `ApiTester` persistence.
- `Auth__ApiKeys__0`: initial API key required by protected API endpoints.
- `AI__OpenAI__ApiKey`: optional OpenAI key. Empty value keeps AI features in stub mode.

### `site` service

- `ASPNETCORE_ENVIRONMENT` (default: `Production`): ASP.NET Core environment.
- `ASPNETCORE_URLS` (default: `http://+:8081`): bind address/port in container.
- `ApiTesterWeb__BaseUrl` (default: `http://web:8080`): internal URL for site -> web API calls.
- `ApiTesterWeb__ApiKey` (default: `dev-api-key`): API key used by the site when calling web API.
- `ConnectionStrings__LeadCapture` (default: `Data Source=/app/data/leads.db`): SQLite path for lead-capture data.
- `ConnectionStrings__Identity` (default: `Data Source=/app/data/identity.db`): SQLite path for site identity data.
- `Auth__Authority` (default: empty): OpenID Connect authority URL (set for real SSO).
- `Auth__ClientId` (default: empty): OpenID Connect client id.
- `Auth__ClientSecret` (default: empty): OpenID Connect client secret.
- `Auth__CallbackPath` (default: `/signin-oidc`): OpenID Connect callback path.

## Ports and volumes

- Host `8080` -> `web:8080`
- Host `8081` -> `site:8081`
- `postgres_data` volume for PostgreSQL durable storage
- `site_data` volume for site SQLite files
