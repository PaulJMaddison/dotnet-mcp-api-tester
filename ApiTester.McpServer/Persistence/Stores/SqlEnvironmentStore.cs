using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlEnvironmentStore : IEnvironmentStore
{
    private readonly ApiTesterDbContext _db;

    public SqlEnvironmentStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<EnvironmentRecord> CreateAsync(string ownerKey, Guid projectId, string name, string baseUrl, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        name = NormalizeName(name);
        baseUrl = NormalizeBaseUrl(baseUrl);

        var existing = await _db.Environments.AsNoTracking()
            .Where(x => x.OwnerKey == ownerKey && x.ProjectId == projectId && x.Name == name)
            .Select(x => new EnvironmentRecord(x.EnvironmentId, x.ProjectId, x.OwnerKey, x.Name, x.BaseUrl, x.CreatedUtc, x.UpdatedUtc))
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
            throw new InvalidOperationException("Environment name already exists.");

        var now = DateTime.UtcNow;
        var entity = new EnvironmentEntity
        {
            EnvironmentId = Guid.NewGuid(),
            ProjectId = projectId,
            OwnerKey = ownerKey,
            Name = name,
            BaseUrl = baseUrl,
            CreatedUtc = now,
            UpdatedUtc = now
        };

        _db.Environments.Add(entity);
        await _db.SaveChangesAsync(ct);
        return MapToRecord(entity);
    }

    public async Task<IReadOnlyList<EnvironmentRecord>> ListAsync(string ownerKey, Guid projectId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        return await _db.Environments.AsNoTracking()
            .Where(x => x.OwnerKey == ownerKey && x.ProjectId == projectId)
            .OrderBy(x => x.Name)
            .Select(x => new EnvironmentRecord(x.EnvironmentId, x.ProjectId, x.OwnerKey, x.Name, x.BaseUrl, x.CreatedUtc, x.UpdatedUtc))
            .ToListAsync(ct);
    }

    public async Task<EnvironmentRecord?> GetAsync(string ownerKey, Guid projectId, Guid environmentId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        return await _db.Environments.AsNoTracking()
            .Where(x => x.OwnerKey == ownerKey && x.ProjectId == projectId && x.EnvironmentId == environmentId)
            .Select(x => new EnvironmentRecord(x.EnvironmentId, x.ProjectId, x.OwnerKey, x.Name, x.BaseUrl, x.CreatedUtc, x.UpdatedUtc))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EnvironmentRecord?> GetByNameAsync(string ownerKey, Guid projectId, string name, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        name = NormalizeName(name);
        return await _db.Environments.AsNoTracking()
            .Where(x => x.OwnerKey == ownerKey && x.ProjectId == projectId && x.Name == name)
            .Select(x => new EnvironmentRecord(x.EnvironmentId, x.ProjectId, x.OwnerKey, x.Name, x.BaseUrl, x.CreatedUtc, x.UpdatedUtc))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<EnvironmentRecord?> UpdateAsync(string ownerKey, Guid projectId, Guid environmentId, string name, string baseUrl, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        name = NormalizeName(name);
        baseUrl = NormalizeBaseUrl(baseUrl);

        var entity = await _db.Environments
            .Where(x => x.OwnerKey == ownerKey && x.ProjectId == projectId && x.EnvironmentId == environmentId)
            .FirstOrDefaultAsync(ct);
        if (entity is null)
            return null;

        var nameClash = await _db.Environments.AsNoTracking()
            .AnyAsync(x => x.OwnerKey == ownerKey && x.ProjectId == projectId && x.EnvironmentId != environmentId && x.Name == name, ct);
        if (nameClash)
            throw new InvalidOperationException("Environment name already exists.");

        entity.Name = name;
        entity.BaseUrl = baseUrl;
        entity.UpdatedUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return MapToRecord(entity);
    }

    public async Task<bool> DeleteAsync(string ownerKey, Guid projectId, Guid environmentId, CancellationToken ct)
    {
        ownerKey = NormalizeOwnerKey(ownerKey);
        var entity = await _db.Environments
            .Where(x => x.OwnerKey == ownerKey && x.ProjectId == projectId && x.EnvironmentId == environmentId)
            .FirstOrDefaultAsync(ct);
        if (entity is null)
            return false;

        _db.Environments.Remove(entity);
        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static EnvironmentRecord MapToRecord(EnvironmentEntity entity)
        => new(entity.EnvironmentId, entity.ProjectId, entity.OwnerKey, entity.Name, entity.BaseUrl, entity.CreatedUtc, entity.UpdatedUtc);

    private static string NormalizeOwnerKey(string ownerKey)
    {
        ownerKey = (ownerKey ?? "").Trim();
        return string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey;
    }

    private static string NormalizeName(string name)
    {
        name = (name ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Environment name is required.", nameof(name));
        return name;
    }

    private static string NormalizeBaseUrl(string baseUrl)
    {
        baseUrl = (baseUrl ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            throw new ArgumentException("Environment baseUrl is required.", nameof(baseUrl));
        return baseUrl.TrimEnd('/');
    }
}
