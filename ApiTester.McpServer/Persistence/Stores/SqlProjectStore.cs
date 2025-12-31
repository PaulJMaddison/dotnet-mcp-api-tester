using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlProjectStore : IProjectStore
{
    private readonly ApiTesterDbContext _db;

    public SqlProjectStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<ProjectRecord> CreateAsync(string name, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));

        var key = ProjectKeyGenerator.FromName(name);

        var existing = await _db.Projects.AsNoTracking()
            .Where(x => x.ProjectKey == key)
            .Select(x => new ProjectRecord(x.ProjectId, x.Name, x.ProjectKey, x.CreatedUtc))
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
            return existing;

        var entity = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            ProjectKey = key,
            Name = name,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Projects.Add(entity);
        try
        {
            await _db.SaveChangesAsync(ct);
            return new ProjectRecord(entity.ProjectId, entity.Name, entity.ProjectKey, entity.CreatedUtc);
        }
        catch (DbUpdateException)
        {
            var fallback = await _db.Projects.AsNoTracking()
                .Where(x => x.ProjectKey == key)
                .Select(x => new ProjectRecord(x.ProjectId, x.Name, x.ProjectKey, x.CreatedUtc))
                .FirstOrDefaultAsync(ct);
            if (fallback is not null)
                return fallback;

            throw;
        }
    }

    public async Task<IReadOnlyList<ProjectRecord>> ListAsync(int take, CancellationToken ct)
    {
        take = take <= 0 ? 50 : Math.Min(take, 200);

        var projects = await _db.Projects
            .AsNoTracking()
            .OrderByDescending(x => x.CreatedUtc)
            .Take(take)
            .Select(x => new ProjectRecord(x.ProjectId, x.Name, x.ProjectKey, x.CreatedUtc))
            .ToListAsync(ct);

        return projects;
    }

    public async Task<ProjectRecord?> GetAsync(Guid projectId, CancellationToken ct)
    {
        return await _db.Projects
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .Select(x => new ProjectRecord(x.ProjectId, x.Name, x.ProjectKey, x.CreatedUtc))
            .FirstOrDefaultAsync(ct);
    }
}
