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

    public async Task<ProjectRecord> CreateAsync(Guid tenantId, string ownerKey, string name, CancellationToken ct)
    {
        name = (name ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));

        tenantId = NormalizeTenantId(tenantId);
        ownerKey = NormalizeOwnerKey(ownerKey);
        var key = ProjectKeyGenerator.FromName(name);

        var existing = await _db.Projects.AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ProjectKey == key)
            .Select(x => new ProjectRecord(x.ProjectId, x.OrganisationId, x.TenantId, x.OwnerKey, x.Name, x.ProjectKey, x.CreatedUtc))
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
            return existing;

        var entity = new ProjectEntity
        {
            ProjectId = Guid.NewGuid(),
            OrganisationId = tenantId,
            TenantId = tenantId,
            OwnerKey = ownerKey,
            ProjectKey = key,
            Name = name,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Projects.Add(entity);
        try
        {
            await _db.SaveChangesAsync(ct);
            return new ProjectRecord(entity.ProjectId, entity.OrganisationId, entity.TenantId, entity.OwnerKey, entity.Name, entity.ProjectKey, entity.CreatedUtc);
        }
        catch (DbUpdateException)
        {
            var fallback = await _db.Projects.AsNoTracking()
                .Where(x => x.TenantId == tenantId && x.ProjectKey == key)
                .Select(x => new ProjectRecord(x.ProjectId, x.OrganisationId, x.TenantId, x.OwnerKey, x.Name, x.ProjectKey, x.CreatedUtc))
                .FirstOrDefaultAsync(ct);
            if (fallback is not null)
                return fallback;

            throw;
        }
    }

    public async Task<PagedResult<ProjectRecord>> ListAsync(Guid tenantId, PageRequest request, SortField sortField, SortDirection direction, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        var baseQuery = _db.Projects.AsNoTracking().Where(x => x.TenantId == tenantId);
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
            .Select(x => new ProjectRecord(x.ProjectId, x.OrganisationId, x.TenantId, x.OwnerKey, x.Name, x.ProjectKey, x.CreatedUtc))
            .ToListAsync(ct);

        int? nextOffset = request.Offset + projects.Count < total
            ? request.Offset + projects.Count
            : null;

        return new PagedResult<ProjectRecord>(projects, total, nextOffset);
    }

    public async Task<ProjectRecord?> GetAsync(Guid tenantId, Guid projectId, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        return await _db.Projects
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId && x.TenantId == tenantId)
            .Select(x => new ProjectRecord(x.ProjectId, x.OrganisationId, x.TenantId, x.OwnerKey, x.Name, x.ProjectKey, x.CreatedUtc))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ProjectRecord?> GetByKeyAsync(Guid tenantId, string projectKey, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        projectKey = NormalizeProjectKey(projectKey);

        return await _db.Projects
            .AsNoTracking()
            .Where(x => x.TenantId == tenantId && x.ProjectKey == projectKey)
            .Select(x => new ProjectRecord(x.ProjectId, x.OrganisationId, x.TenantId, x.OwnerKey, x.Name, x.ProjectKey, x.CreatedUtc))
            .FirstOrDefaultAsync(ct);
    }

    public Task<bool> ExistsAsync(Guid tenantId, Guid projectId, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        return _db.Projects.AnyAsync(x => x.ProjectId == projectId && x.TenantId == tenantId, ct);
    }

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

    private static Guid NormalizeTenantId(Guid tenantId)
        => tenantId == Guid.Empty ? OrgDefaults.DefaultOrganisationId : tenantId;
}
