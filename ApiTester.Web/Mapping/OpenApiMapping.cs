using ApiTester.McpServer.Models;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class OpenApiMapping
{
    public static OpenApiSpecMetadataDto ToMetadataDto(OpenApiSpecRecord record)
        => new(record.ProjectId, record.Title, record.Version, record.CreatedUtc);
}
