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

    public async Task<ProjectRecord> CreateAsync(Guid organisationId, string ownerKey, string name, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));

        organisationId = NormalizeOrganisationId(organisationId);
        ownerKey = NormalizeOwnerKey(ownerKey);
        var key = ProjectKeyGenerator.FromName(name);

        var existing = await _db.Projects.AsNoTracking()
            .Where(x => x.OrganisationId == organisationId && x.ProjectKey == key)
            .Select(x => new ProjectRecord(x.ProjectId, x.OrganisationId, x.OwnerKey, x.Name, x.ProjectKey, x.CreatedUtc))
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
            return existing;

        var entity = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            OrganisationId = organisationId,
            OwnerKey = ownerKey,
            ProjectKey = key,
            Name = name,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Projects.Add(entity);
        try
        {
            await _db.SaveChangesAsync(ct);
            return new ProjectRecord(entity.ProjectId, entity.OrganisationId, entity.OwnerKey, entity.Name, entity.ProjectKey, entity.CreatedUtc);
        }
        catch (DbUpdateException)
        {
            var fallback = await _db.Projects.AsNoTracking()
                .Where(x => x.OrganisationId == organisationId && x.ProjectKey == key)
                .Select(x => new ProjectRecord(x.ProjectId, x.OrganisationId, x.OwnerKey, x.Name, x.ProjectKey, x.CreatedUtc))
                .FirstOrDefaultAsync(ct);
            if (fallback is not null)
                return fallback;

            throw;
        }
    }

    public async Task<PagedResult<ProjectRecord>> ListAsync(Guid organisationId, PageRequest request, SortField sortField, SortDirection direction, CancellationToken ct)
    {
        organisationId = NormalizeOrganisationId(organisationId);
        var baseQuery = _db.Projects.AsNoTracking().Where(x => x.OrganisationId == organisationId);
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
            .Select(x => new ProjectRecord(x.ProjectId, x.OrganisationId, x.OwnerKey, x.Name, x.ProjectKey, x.CreatedUtc))
            .ToListAsync(ct);

        int? nextOffset = request.Offset + projects.Count < total
            ? request.Offset + projects.Count
            : null;

        return new PagedResult<ProjectRecord>(projects, total, nextOffset);
    }

    public async Task<ProjectRecord?> GetAsync(Guid organisationId, Guid projectId, CancellationToken ct)
    {
        organisationId = NormalizeOrganisationId(organisationId);
        return await _db.Projects
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.OrganisationId == organisationId)
            .Select(x => new ProjectRecord(x.ProjectId, x.OrganisationId, x.OwnerKey, x.Name, x.ProjectKey, x.CreatedUtc))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ProjectRecord?> GetByKeyAsync(Guid organisationId, string projectKey, CancellationToken ct)
    {
        organisationId = NormalizeOrganisationId(organisationId);
        projectKey = NormalizeProjectKey(projectKey);

        return await _db.Projects
            .AsNoTracking()
            .Where(x => x.OrganisationId == organisationId && x.ProjectKey == projectKey)
            .Select(x => new ProjectRecord(x.ProjectId, x.OrganisationId, x.OwnerKey, x.Name, x.ProjectKey, x.CreatedUtc))
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> ExistsAsync(Guid projectId, CancellationToken ct)
        => _db.Projects.AnyAsync(x => x.ProjectId == projectId, ct);

    private static string NormalizeOwnerKey(string ownerKey)
    {
        ownerKey = (ownerKey ?? "").Trim();
        return string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey;
    }

    private static string NormalizeProjectKey(string projectKey)
    {
        projectKey = (projectKey ?? "").Trim();
        return string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey;
    }

    private static Guid NormalizeOrganisationId(Guid organisationId)
        => organisationId == Guid.Empty ? OrgDefaults.DefaultOrganisationId : organisationId;
}
