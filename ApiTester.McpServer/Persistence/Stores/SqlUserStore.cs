using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlUserStore : IUserStore
{
    private readonly ApiTesterDbContext _db;

    public SqlUserStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<UserRecord> CreateAsync(string externalId, string displayName, string? email, CancellationToken ct)
    {
        externalId = (externalId ?? string.Empty).Trim();
        displayName = (displayName ?? string.Empty).Trim();
        email = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

        if (string.IsNullOrWhiteSpace(externalId))
            throw new ArgumentException("External ID is required.", nameof(externalId));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));

        var existing = await _db.Users.AsNoTracking()
            .Where(u => u.ExternalId == externalId)
            .Select(u => new UserRecord(u.UserId, u.ExternalId, u.DisplayName, u.Email, u.CreatedUtc))
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
            return existing;

        var entity = new UserEntity
        {
            UserId = Guid.NewGuid(),
            ExternalId = externalId,
            DisplayName = displayName,
            Email = email,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Users.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new UserRecord(entity.UserId, entity.ExternalId, entity.DisplayName, entity.Email, entity.CreatedUtc);
    }

    public async Task<UserRecord?> GetAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Users.AsNoTracking()
            .Where(u => u.UserId == userId)
            .Select(u => new UserRecord(u.UserId, u.ExternalId, u.DisplayName, u.Email, u.CreatedUtc))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<UserRecord?> GetByExternalIdAsync(string externalId, CancellationToken ct)
    {
        externalId = (externalId ?? string.Empty).Trim();
        return await _db.Users.AsNoTracking()
            .Where(u => u.ExternalId == externalId)
            .Select(u => new UserRecord(u.UserId, u.ExternalId, u.DisplayName, u.Email, u.CreatedUtc))
            .FirstOrDefaultAsync(ct);
    }
}
