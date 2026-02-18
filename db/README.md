# Database schema script

This folder contains a single-source-of-truth SQL Server schema script generated from EF Core migrations.

## Files

- `schema.sql`: idempotent migration script that can be run repeatedly.

## Regenerate

From the repository root:

```bash
dotnet ef migrations script --idempotent \
  --project ApiTester.McpServer/ApiTester.McpServer.csproj \
  --startup-project ApiTester.McpServer/ApiTester.McpServer.csproj \
  --output db/schema.sql
```

## Apply to a clean database

Using `sqlcmd`:

```bash
sqlcmd -S <server> -d <database> -i db/schema.sql
```

The script contains migration history checks, so it is safe to run on a new or partially migrated database.
