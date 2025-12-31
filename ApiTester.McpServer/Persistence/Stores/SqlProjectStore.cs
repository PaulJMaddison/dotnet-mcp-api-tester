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

    public async Task<PagedResult<ProjectRecord>> ListAsync(PageRequest request, SortField sortField, SortDirection direction, CancellationToken ct)
    {
        var baseQuery = _db.Projects.AsNoTracking();
        var total = await baseQuery.CountAsync(ct);

        var ordered = sortField switch
        {
            SortField.StartedUtc => direction == SortDirection.Asc
                ? baseQuery.OrderBy(x => x.CreatedUtc)
                : baseQuery.OrderByDescending(x => x.CreatedUtc),
            _ => direction == SortDirection.Asc
                ? baseQuery.OrderBy(x => x.CreatedUtc)
                : baseQuery.OrderByDescending(x => x.CreatedUtc)
        };

        var projects = await ordered
            .Skip(request.Offset)
            .Take(request.PageSize)
            .Select(x => new ProjectRecord(x.ProjectId, x.Name, x.ProjectKey, x.CreatedUtc))
            .ToListAsync(ct);

        int? nextOffset = request.Offset + projects.Count < total
            ? request.Offset + projects.Count
            : null;

        return new PagedResult<ProjectRecord>(projects, total, nextOffset);
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
