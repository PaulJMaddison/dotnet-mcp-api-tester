using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlGeneratedDocsStore : IGeneratedDocsStore
{
    private readonly ApiTesterDbContext _db;

    public SqlGeneratedDocsStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<GeneratedDocsRecord?> GetAsync(Guid organisationId, Guid projectId, CancellationToken ct)
    {
        return await _db.GeneratedDocs.AsNoTracking()
            .Where(docs => docs.OrganisationId == organisationId && docs.ProjectId == projectId)
            .Select(docs => new GeneratedDocsRecord(
                docs.DocsId,
                docs.OrganisationId,
                docs.ProjectId,
                docs.SpecId,
                docs.DocsJson,
                docs.CreatedUtc,
                docs.UpdatedUtc))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<GeneratedDocsRecord> UpsertAsync(
        Guid organisationId,
        Guid projectId,
        Guid specId,
        string docsJson,
        DateTime generatedUtc,
        CancellationToken ct)
    {
        docsJson = (docsJson ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(docsJson))
            throw new ArgumentException("Generated docs payload is required.", nameof(docsJson));

        var entity = await _db.GeneratedDocs
            .FirstOrDefaultAsync(docs => docs.OrganisationId == organisationId && docs.ProjectId == projectId, ct);

        if (entity is null)
        {
            entity = new GeneratedDocsEntity
            {
                DocsId = Guid.NewGuid(),
                OrganisationId = organisationId,
                ProjectId = projectId,
                SpecId = specId,
                DocsJson = docsJson,
                CreatedUtc = generatedUtc,
                UpdatedUtc = generatedUtc
            };
            _db.GeneratedDocs.Add(entity);
        }
        else
        {
            entity.SpecId = specId;
            entity.DocsJson = docsJson;
            entity.UpdatedUtc = generatedUtc;
        }

        await _db.SaveChangesAsync(ct);

        return new GeneratedDocsRecord(
            entity.DocsId,
            entity.OrganisationId,
            entity.ProjectId,
            entity.SpecId,
            entity.DocsJson,
            entity.CreatedUtc,
            entity.UpdatedUtc);
    }
}
