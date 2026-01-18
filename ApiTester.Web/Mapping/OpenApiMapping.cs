using ApiTester.McpServer.Models;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class OpenApiMapping
{
    public static OpenApiSpecMetadataDto ToMetadataDto(OpenApiSpecRecord record)
        => new(record.SpecId, record.ProjectId, record.Title, record.Version, record.SpecHash, record.CreatedUtc);

    public static OpenApiSpecDetailDto ToDetailDto(OpenApiSpecRecord record)
        => new(record.SpecId, record.ProjectId, record.Title, record.Version, record.SpecJson, record.SpecHash, record.CreatedUtc);
}
