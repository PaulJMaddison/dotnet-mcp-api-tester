using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Stores;

public interface IAuditEventStore
{
    Task<AuditEventRecord> CreateAsync(AuditEventRecord record, CancellationToken ct);

    Task<IReadOnlyList<AuditEventRecord>> ListAsync(
        Guid organisationId,
        int take,
        string? action,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct);
}
