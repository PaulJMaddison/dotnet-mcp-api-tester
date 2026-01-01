using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlRunAnnotationStore : IRunAnnotationStore
{
    private readonly ApiTesterDbContext _db;

    public SqlRunAnnotationStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<RunAnnotationRecord>> ListAsync(string ownerKey, Guid runId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);

        var annotations = await _db.RunAnnotations
            .AsNoTracking()
            .Where(a => a.RunId == runId && a.OwnerKey == ownerKey)
            .OrderBy(a => a.CreatedUtc)
            .ToListAsync(ct);

        return annotations.Select(MapToRecord).ToList();
    }

    public async Task<RunAnnotationRecord?> GetAsync(string ownerKey, Guid runId, Guid annotationId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);

        var annotation = await _db.RunAnnotations
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.RunId == runId && a.AnnotationId == annotationId && a.OwnerKey == ownerKey, ct);

        return annotation is null ? null : MapToRecord(annotation);
    }

    public async Task<RunAnnotationRecord> CreateAsync(string ownerKey, Guid runId, string note, string? jiraLink, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);

        var runExists = await _db.TestRuns
            .Include(r => r.Project)
            .AnyAsync(r => r.RunId == runId && r.Project != null && r.Project.OwnerKey == ownerKey, ct);
        if (!runExists)
            throw new InvalidOperationException("Run not found.");

        var now = DateTime.UtcNow;
        var entity = new RunAnnotationEntity
        {
            AnnotationId = Guid.NewGuid(),
            RunId = runId,
            OwnerKey = ownerKey,
            Note = note,
            JiraLink = jiraLink,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        _db.RunAnnotations.Add(entity);
        await _db.SaveChangesAsync(ct);

        return MapToRecord(entity);
    }

    public async Task<RunAnnotationRecord?> UpdateAsync(string ownerKey, Guid runId, Guid annotationId, string note, string? jiraLink, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);

        var entity = await _db.RunAnnotations
            .FirstOrDefaultAsync(a => a.RunId == runId && a.AnnotationId == annotationId && a.OwnerKey == ownerKey, ct);

        if (entity is null)
            return null;

        entity.Note = note;
        entity.JiraLink = jiraLink;
        entity.UpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        return MapToRecord(entity);
    }

    public async Task<bool> DeleteAsync(string ownerKey, Guid runId, Guid annotationId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);

        var entity = await _db.RunAnnotations
            .FirstOrDefaultAsync(a => a.RunId == runId && a.AnnotationId == annotationId && a.OwnerKey == ownerKey, ct);

        if (entity is null)
            return false;

        _db.RunAnnotations.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static RunAnnotationRecord MapToRecord(RunAnnotationEntity entity) =>
        new(
            entity.AnnotationId,
            entity.RunId,
            entity.OwnerKey,
            entity.Note,
            entity.JiraLink,
            entity.CreatedUtc,
            entity.UpdatedUtc);

    private static string NormalizeOwnerKey(string ownerKey)
    {
        ownerKey = (ownerKey ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey;
    }
}
