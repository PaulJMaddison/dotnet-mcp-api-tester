using System.Linq;
using ApiTester.McpServer.Models;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class AuditMapping
{
    public static AuditEventResponse ToDto(AuditEventRecord record)
        => new(
            record.OrganisationId,
            record.ActorUserId,
            record.Action,
            record.TargetType,
            record.TargetId,
            record.CreatedUtc,
            record.MetadataJson);

    public static AuditListResponse ToListResponse(IReadOnlyList<AuditEventRecord> records)
        => new(records.Select(ToDto).ToList());
}
