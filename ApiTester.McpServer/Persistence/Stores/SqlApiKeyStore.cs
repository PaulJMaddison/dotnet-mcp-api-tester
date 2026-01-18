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
            Hash = hash,
            Prefix = prefix
        };

        _db.ApiKeys.Add(entity);
        await _db.SaveChangesAsync(ct);

        return new ApiKeyRecord(entity.KeyId, entity.OrganisationId, entity.UserId, entity.Name, entity.Scopes, entity.ExpiresUtc, entity.RevokedUtc, entity.Hash, entity.Prefix);
    }

    public async Task<IReadOnlyList<ApiKeyRecord>> ListAsync(Guid organisationId, CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            return Array.Empty<ApiKeyRecord>();

        return await _db.ApiKeys.AsNoTracking()
            .Where(x => x.OrganisationId == organisationId)
            .OrderBy(x => x.Name)
            .Select(x => new ApiKeyRecord(x.KeyId, x.OrganisationId, x.UserId, x.Name, x.Scopes, x.ExpiresUtc, x.RevokedUtc, x.Hash, x.Prefix))
            .ToListAsync(ct);
    }

    public async Task<ApiKeyRecord?> GetAsync(Guid organisationId, Guid keyId, CancellationToken ct)
    {
        if (organisationId == Guid.Empty || keyId == Guid.Empty)
            return null;

        return await _db.ApiKeys.AsNoTracking()
            .Where(x => x.OrganisationId == organisationId && x.KeyId == keyId)
            .Select(x => new ApiKeyRecord(x.KeyId, x.OrganisationId, x.UserId, x.Name, x.Scopes, x.ExpiresUtc, x.RevokedUtc, x.Hash, x.Prefix))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ApiKeyRecord?> GetByPrefixAsync(string prefix, CancellationToken ct)
    {
        prefix = (prefix ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(prefix))
            return null;

        return await _db.ApiKeys.AsNoTracking()
            .Where(x => x.Prefix == prefix)
            .Select(x => new ApiKeyRecord(x.KeyId, x.OrganisationId, x.UserId, x.Name, x.Scopes, x.ExpiresUtc, x.RevokedUtc, x.Hash, x.Prefix))
            .FirstOrDefaultAsync(ct);
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

        return new ApiKeyRecord(entity.KeyId, entity.OrganisationId, entity.UserId, entity.Name, entity.Scopes, entity.ExpiresUtc, entity.RevokedUtc, entity.Hash, entity.Prefix);
    }
}
