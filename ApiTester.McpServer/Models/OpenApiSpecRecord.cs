namespace ApiTester.McpServer.Models;

public sealed record OpenApiSpecRecord(
    Guid SpecId,
    Guid ProjectId,
    Guid TenantId,
    string Title,
    string Version,
    string SpecJson,
    string SpecHash,
    DateTime CreatedUtc);
