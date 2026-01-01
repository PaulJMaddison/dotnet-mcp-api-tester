using ApiTester.McpServer.Models;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class RunAnnotationMapping
{
    public static RunAnnotationDto ToDto(RunAnnotationRecord record) =>
        new(
            record.AnnotationId,
            record.RunId,
            record.Note,
            record.JiraLink,
            record.CreatedUtc,
            record.UpdatedUtc);

    public static RunAnnotationListResponse ToListResponse(Guid runId, IReadOnlyList<RunAnnotationRecord> records) =>
        new(runId, records.Select(ToDto).ToList());
}
