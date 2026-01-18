using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlAuditEventStore : IAuditEventStore
{
    private readonly ApiTesterDbContext _db;

    public SqlAuditEventStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<AuditEventRecord> CreateAsync(AuditEventRecord record, CancellationToken ct)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));

        var entity = new AuditEventEntity
        {
            AuditEventId = record.AuditEventId == Guid.Empty ? Guid.NewGuid() : record.AuditEventId,
            OrganisationId = record.OrganisationId,
            ActorUserId = record.ActorUserId,
            Action = record.Action.Trim(),
            TargetType = record.TargetType.Trim(),
            TargetId = record.TargetId.Trim(),
            CreatedUtc = record.CreatedUtc == default ? DateTime.UtcNow : record.CreatedUtc,
            MetadataJson = record.MetadataJson
        };

        _db.AuditEvents.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new AuditEventRecord(
            entity.AuditEventId,
            entity.OrganisationId,
            entity.ActorUserId,
            entity.Action,
            entity.TargetType,
            entity.TargetId,
            entity.CreatedUtc,
            entity.MetadataJson);
    }

    public async Task<IReadOnlyList<AuditEventRecord>> ListAsync(
        Guid organisationId,
        int take,
        string? action,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct)
    {
        var query = _db.AuditEvents.AsNoTracking()
            .Where(x => x.OrganisationId == organisationId);

        if (!string.IsNullOrWhiteSpace(action))
        {
            var normalized = action.Trim();
            query = query.Where(x => x.Action == normalized);
        }

        if (fromUtc.HasValue)
            query = query.Where(x => x.CreatedUtc >= fromUtc.Value);

        if (toUtc.HasValue)
            query = query.Where(x => x.CreatedUtc <= toUtc.Value);

        var items = await query
            .OrderByDescending(x => x.CreatedUtc)
            .Take(take)
            .Select(x => new AuditEventRecord(
                x.AuditEventId,
                x.OrganisationId,
                x.ActorUserId,
                x.Action,
                x.TargetType,
                x.TargetId,
                x.CreatedUtc,
                x.MetadataJson))
            .ToListAsync(ct);

        return items;
    }
}
