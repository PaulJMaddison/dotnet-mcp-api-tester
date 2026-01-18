# ApiTester Web API

## Authentication

All routes except `/health` require an API key via the `X-Api-Key` header. Configure one or more keys in configuration:

- `Auth:ApiKey` for a single key
- `Auth:ApiKeys` for multiple keys

Example `appsettings.json` snippet:
```json
{
  "Auth": {
    "ApiKey": "dev-local-key"
  }
}
```

Environment variable example:
```
Auth__ApiKey=dev-local-key
```

Requests must include the header:
```
X-Api-Key: dev-local-key
```

## UI setup

The UI also expects `Auth:ApiKey` (or `Auth:ApiKeys`) configured so it can authenticate incoming requests and call the Web API. Set `ApiTesterWeb:BaseUrl` to the Web API address.

## Projects

### List projects

`GET /api/projects`

Query parameters:
- `pageSize` (optional)
- `pageToken` (optional)
- `sort` (`createdUtc`)
- `order` (`asc` or `desc`)

### Create a project

`POST /api/projects`

Body:
```json
{ "name": "Project name" }
```

### Get a project

`GET /api/projects/{projectId}`

## OpenAPI import

### Import an OpenAPI spec

`POST /api/projects/{projectId}/openapi/import`

Accepts either:
- `multipart/form-data` with a file upload (field name `file`)
- `application/json` with a local server path:
  ```json
  { "path": "/path/to/openapi.json" }
  ```

Response:
```json
{
  "projectId": "uuid",
  "title": "Sample API",
  "version": "1.0.0",
  "createdUtc": "2025-01-01T00:00:00Z"
}
```

Notes:
- Max spec size: 1,000,000 bytes.

### Get OpenAPI metadata

`GET /api/projects/{projectId}/openapi`

Returns the stored spec metadata if available.

## Runs

### List runs

`GET /api/runs?projectKey=...`

### Get run details

`GET /api/runs/{runId}`
