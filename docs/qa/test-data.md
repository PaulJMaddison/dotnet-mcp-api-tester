# E2E test data (FixtureApi)

FixtureApi is available in compose as `http://fixtureapi:8080` (host mapped to `http://localhost:18082`).

## Endpoints
- `GET /health` -> `{ "status": "ok" }`
- `GET /v1/ping` -> `{ "ok": true }`
- `GET /v1/widgets/{id}` -> stable JSON payload
- `POST /v1/widgets` -> echoes object with deterministic id fallback
- `GET /openapi.json` -> OpenAPI 3.0 spec used for import

## Sample calls

```bash
curl http://localhost:18082/health
curl http://localhost:18082/v1/ping
curl http://localhost:18082/v1/widgets/42
curl -X POST http://localhost:18082/v1/widgets -H 'Content-Type: application/json' -d '{"name":"widget-e2e"}'
curl http://localhost:18082/openapi.json
```
