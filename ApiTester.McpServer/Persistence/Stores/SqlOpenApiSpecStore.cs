using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlOpenApiSpecStore : IOpenApiSpecStore
{
    private readonly ApiTesterDbContext _db;

    public SqlOpenApiSpecStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<OpenApiSpecRecord?> GetAsync(Guid projectId, CancellationToken ct)
    {
        var entity = await _db.OpenApiSpecs
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedUtc)
            .FirstOrDefaultAsync(ct);

        return entity is null
            ? null
            : new OpenApiSpecRecord(entity.SpecId, entity.ProjectId, entity.Title, entity.Version, entity.SpecJson, entity.SpecHash, entity.CreatedUtc);
    }

    public async Task<IReadOnlyList<OpenApiSpecRecord>> ListAsync(Guid projectId, CancellationToken ct)
    {
        var entities = await _db.OpenApiSpecs
            .AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedUtc)
            .ToListAsync(ct);

        return entities
            .Select(entity => new OpenApiSpecRecord(entity.SpecId, entity.ProjectId, entity.Title, entity.Version, entity.SpecJson, entity.SpecHash, entity.CreatedUtc))
            .ToList();
    }

    public async Task<OpenApiSpecRecord?> GetByIdAsync(Guid specId, CancellationToken ct)
    {
        var entity = await _db.OpenApiSpecs
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.SpecId == specId, ct);

        return entity is null
            ? null
            : new OpenApiSpecRecord(entity.SpecId, entity.ProjectId, entity.Title, entity.Version, entity.SpecJson, entity.SpecHash, entity.CreatedUtc);
    }

    public async Task<OpenApiSpecRecord> UpsertAsync(Guid projectId, string title, string version, string specJson, string specHash, DateTime createdUtc, CancellationToken ct)
    {
        var entity = await _db.OpenApiSpecs
            .FirstOrDefaultAsync(s => s.ProjectId == projectId && s.SpecHash == specHash, ct);

        if (entity is null)
        {
            entity = new OpenApiSpecEntity
            {
                SpecId = Guid.NewGuid(),
                ProjectId = projectId,
                SpecHash = specHash,
                Title = title,
                Version = version,
                SpecJson = specJson,
                CreatedUtc = createdUtc
            };
            _db.OpenApiSpecs.Add(entity);
        }

        await _db.SaveChangesAsync(ct);

        return new OpenApiSpecRecord(entity.SpecId, entity.ProjectId, entity.Title, entity.Version, entity.SpecJson, entity.SpecHash, entity.CreatedUtc);
    }

    public async Task<bool> DeleteAsync(Guid specId, CancellationToken ct)
    {
        var entity = await _db.OpenApiSpecs.FirstOrDefaultAsync(s => s.SpecId == specId, ct);
        if (entity is null)
            return false;

        _db.OpenApiSpecs.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
