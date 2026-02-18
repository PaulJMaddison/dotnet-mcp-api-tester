using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlApiKeyStore : IApiKeyStore
{
    private readonly ApiTesterDbContext _db;

    public SqlApiKeyStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<ApiKeyRecord> CreateAsync(
        Guid organisationId,
        Guid userId,
        string name,
        string scopes,
        DateTime? expiresUtc,
        string hash,
        string prefix,
        CancellationToken ct)
    {
        name = (name ?? string.Empty).Trim();
        scopes = (scopes ?? string.Empty).Trim();
        hash = (hash ?? string.Empty).Trim();
        prefix = (prefix ?? string.Empty).Trim();

        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(scopes))
            throw new ArgumentException("Scopes are required.", nameof(scopes));
        if (string.IsNullOrWhiteSpace(hash))
            throw new ArgumentException("Hash is required.", nameof(hash));
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix is required.", nameof(prefix));

        var entity = new ApiKeyEntity
        {
            KeyId = Guid.NewGuid(),
            OrganisationId = organisationId,
            UserId = userId,
            Name = name,
            Scopes = scopes,
            ExpiresUtc = expiresUtc,
            RevokedUtc = null,
            LastUsedUtc = null,
            Hash = hash,
            Prefix = prefix
        };

        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync(ct);

        return ToRecord(entity);
    }

    public async Task<IReadOnlyList<ApiKeyRecord>> ListAsync(Guid organisationId, CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            return Array.Empty<ApiKeyRecord>();

        return await _db.ApiKeys.AsNoTracking()
            .Where(x => x.OrganisationId == organisationId)
            .OrderBy(x => x.Name)
            .Select(ToRecordExpression())
            .ToListAsync(ct);
    }

    public async Task<ApiKeyRecord?> GetAsync(Guid organisationId, Guid keyId, CancellationToken ct)
    {
        if (organisationId == Guid.Empty || keyId == Guid.Empty)
            return null;

        return await _db.ApiKeys.AsNoTracking()
            .Where(x => x.OrganisationId == organisationId && x.KeyId == keyId)
            .Select(ToRecordExpression())
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ApiKeyRecord?> GetByPrefixAsync(string prefix, CancellationToken ct)
    {
        prefix = (prefix ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            return null;

        return await _db.ApiKeys.AsNoTracking()
            .Where(x => x.Prefix == prefix)
            .OrderBy(x => x.KeyId)
            .Select(ToRecordExpression())
            .FirstOrDefaultAsync(ct);
    }


    public async Task<IReadOnlyList<ApiKeyRecord>> ListByPrefixAsync(string prefix, CancellationToken ct)
    {
        prefix = (prefix ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            return Array.Empty<ApiKeyRecord>();

        return await _db.ApiKeys.AsNoTracking()
            .Where(x => x.Prefix == prefix)
            .Select(ToRecordExpression())
            .ToListAsync(ct);
    }
    public async Task<ApiKeyRecord?> RevokeAsync(Guid organisationId, Guid keyId, DateTime revokedUtc, CancellationToken ct)
    {
        if (organisationId == Guid.Empty || keyId == Guid.Empty)
            return null;

        var entity = await _db.ApiKeys.FirstOrDefaultAsync(x => x.OrganisationId == organisationId && x.KeyId == keyId, ct);
        if (entity is null)
            return null;

        if (!entity.RevokedUtc.HasValue)
        {
            entity.RevokedUtc = revokedUtc;
            await _db.SaveChangesAsync(ct);
        }

        return ToRecord(entity);
    }

    public async Task<ApiKeyRecord?> TouchLastUsedAsync(Guid keyId, DateTime lastUsedUtc, CancellationToken ct)
    {
        if (keyId == Guid.Empty)
            return null;

        var entity = await _db.ApiKeys.FirstOrDefaultAsync(x => x.KeyId == keyId, ct);
        if (entity is null)
            return null;

        entity.LastUsedUtc = lastUsedUtc;
        await _db.SaveChangesAsync(ct);
        return ToRecord(entity);
    }

    private static ApiKeyRecord ToRecord(ApiKeyEntity x)
        => new(x.KeyId, x.OrganisationId, x.UserId, x.Name, x.Scopes, x.ExpiresUtc, x.RevokedUtc, x.LastUsedUtc, x.Hash, x.Prefix);

    private static System.Linq.Expressions.Expression<Func<ApiKeyEntity, ApiKeyRecord>> ToRecordExpression()
        => x => new ApiKeyRecord(x.KeyId, x.OrganisationId, x.UserId, x.Name, x.Scopes, x.ExpiresUtc, x.RevokedUtc, x.LastUsedUtc, x.Hash, x.Prefix);
}
