using ApiTester.McpServer.Models;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class EnvironmentMapping
{
    public static EnvironmentDto ToDto(EnvironmentRecord record)
        => new(record.EnvironmentId, record.ProjectId, record.Name, record.BaseUrl, record.CreatedUtc, record.UpdatedUtc);

    public static EnvironmentListResponse ToListResponse(IReadOnlyList<EnvironmentRecord> records)
        => new(records.Select(ToDto).ToList());
}
