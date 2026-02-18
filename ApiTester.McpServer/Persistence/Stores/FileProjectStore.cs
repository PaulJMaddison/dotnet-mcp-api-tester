using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileProjectStore : IProjectStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileProjectStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "run-history", "projects.json");

    public async Task<ProjectRecord> CreateAsync(Guid tenantId, string ownerKey, string name, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        ownerKey = NormalizeOwnerKey(ownerKey);
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Project name is required.", nameof(name));

        var key = ProjectKeyGenerator.FromName(name);

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var existing = list.FirstOrDefault(p => p.TenantId == tenantId && p.ProjectKey.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
                return existing;

            var record = new ProjectRecord(Guid.NewGuid(), tenantId, tenantId, ownerKey, name, key, DateTime.UtcNow);
            list.Add(record);
            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<PagedResult<ProjectRecord>> ListAsync(Guid tenantId, PageRequest request, SortField sortField, SortDirection direction, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        var list = await LoadAsync(ct);
        list = list.Where(p => p.TenantId == tenantId).ToList();

        var ordered = sortField switch
        {
            SortField.StartedUtc => direction == SortDirection.Asc
                ? list.OrderBy(p => p.CreatedUtc)
                : list.OrderByDescending(p => p.CreatedUtc),
            _ => direction == SortDirection.Asc
                ? list.OrderBy(p => p.CreatedUtc)
                : list.OrderByDescending(p => p.CreatedUtc)
        };

        var total = list.Count;
        var page = ordered
            .Skip(request.Offset)
            .Take(request.PageSize)
            .ToList();

        int? nextOffset = Paging.CalculateNextOffset(request.Offset, page.Count, total);

        return new PagedResult<ProjectRecord>(page, total, nextOffset);
    }

    public async Task<ProjectRecord?> GetAsync(Guid tenantId, Guid projectId, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(p => p.ProjectId == projectId && p.TenantId == tenantId);
    }

    public async Task<ProjectRecord?> GetByKeyAsync(Guid tenantId, string projectKey, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        projectKey = NormalizeProjectKey(projectKey);
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(p => p.TenantId == tenantId && p.ProjectKey.Equals(projectKey, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<bool> ExistsAsync(Guid tenantId, Guid projectId, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        var list = await LoadAsync(ct);
        return list.Any(p => p.ProjectId == projectId && p.TenantId == tenantId);
    }

    public async Task<bool> ExistsAnyAsync(Guid projectId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.Any(p => p.ProjectId == projectId);
    }

    private async Task<List<ProjectRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        var list = JsonSerializer.Deserialize<List<ProjectRecord>>(json, JsonDefaults.Default) ?? [];
        return list.Select(NormalizeOrg).ToList();
    }

    private async Task SaveAsync(List<ProjectRecord> list, CancellationToken ct)
    {
        var dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }

    private static ProjectRecord NormalizeOrg(ProjectRecord record)
    {
        var normalizedOrg = record.OrganisationId == Guid.Empty
            ? OrgDefaults.DefaultOrganisationId
            : record.OrganisationId;
        var normalizedTenant = record.TenantId == Guid.Empty ? normalizedOrg : record.TenantId;
        var normalizedOwnerKey = string.IsNullOrWhiteSpace(record.OwnerKey)
            ? OwnerKeyDefaults.Default
            : record.OwnerKey;

        if (normalizedOrg == record.OrganisationId
            && normalizedTenant == record.TenantId
            && normalizedOwnerKey == record.OwnerKey)
        {
            return record;
        }

        return record with
        {
            OrganisationId = normalizedOrg,
            TenantId = normalizedTenant,
            OwnerKey = normalizedOwnerKey
        };
    }

    private static string NormalizeOwnerKey(string ownerKey)
    {
        ownerKey = (ownerKey ?? "").Trim();
        return string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey;
    }

    private static Guid NormalizeTenantId(Guid tenantId)
        => tenantId == Guid.Empty ? OrgDefaults.DefaultOrganisationId : tenantId;

    private static string NormalizeProjectKey(string projectKey)
    {
        projectKey = (projectKey ?? "").Trim();
        return string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey;
    }
}
