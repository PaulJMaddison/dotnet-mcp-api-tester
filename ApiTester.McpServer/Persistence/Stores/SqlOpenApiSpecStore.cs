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
            .FirstOrDefaultAsync(s => s.ProjectId == projectId, ct);

        return entity is null
            ? null
            : new OpenApiSpecRecord(entity.SpecId, entity.ProjectId, entity.Title, entity.Version, entity.SpecJson, entity.CreatedUtc);
    }

    public async Task<OpenApiSpecRecord> UpsertAsync(Guid projectId, string title, string version, string specJson, DateTime createdUtc, CancellationToken ct)
    {
        var entity = await _db.OpenApiSpecs.FirstOrDefaultAsync(s => s.ProjectId == projectId, ct);
        if (entity is null)
        {
            entity = new OpenApiSpecEntity
            {
                SpecId = Guid.NewGuid(),
                ProjectId = projectId
            };
            _db.OpenApiSpecs.Add(entity);
        }

        entity.Title = title;
        entity.Version = version;
        entity.SpecJson = specJson;
        entity.CreatedUtc = createdUtc;

        await _db.SaveChangesAsync(ct);

        return new OpenApiSpecRecord(entity.SpecId, entity.ProjectId, entity.Title, entity.Version, entity.SpecJson, entity.CreatedUtc);
    }
}
