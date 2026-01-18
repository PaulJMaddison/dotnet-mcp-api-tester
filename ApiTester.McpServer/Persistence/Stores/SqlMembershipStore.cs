using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlMembershipStore : IMembershipStore
{
    private readonly ApiTesterDbContext _db;

    public SqlMembershipStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<MembershipRecord> CreateAsync(Guid organisationId, Guid userId, OrgRole role, CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));
        if (userId == Guid.Empty)
            throw new ArgumentException("User ID is required.", nameof(userId));

        var existing = await _db.Memberships.AsNoTracking()
            .Where(m => m.OrganisationId == organisationId && m.UserId == userId)
            .Select(m => new MembershipRecord(m.OrganisationId, m.UserId, m.Role, m.CreatedUtc))
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
            return existing;

        var entity = new MembershipEntity
        {
            OrganisationId = organisationId,
            UserId = userId,
            Role = role,
            CreatedUtc = DateTime.UtcNow
        };

        _db.Memberships.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new MembershipRecord(entity.OrganisationId, entity.UserId, entity.Role, entity.CreatedUtc);
    }

    public async Task<MembershipRecord?> GetAsync(Guid organisationId, Guid userId, CancellationToken ct)
    {
        return await _db.Memberships.AsNoTracking()
            .Where(m => m.OrganisationId == organisationId && m.UserId == userId)
            .Select(m => new MembershipRecord(m.OrganisationId, m.UserId, m.Role, m.CreatedUtc))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<MembershipRecord>> ListByOrganisationAsync(Guid organisationId, CancellationToken ct)
    {
        return await _db.Memberships.AsNoTracking()
            .Where(m => m.OrganisationId == organisationId)
            .Select(m => new MembershipRecord(m.OrganisationId, m.UserId, m.Role, m.CreatedUtc))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MembershipRecord>> ListByUserAsync(Guid userId, CancellationToken ct)
    {
        return await _db.Memberships.AsNoTracking()
            .Where(m => m.UserId == userId)
            .Select(m => new MembershipRecord(m.OrganisationId, m.UserId, m.Role, m.CreatedUtc))
            .ToListAsync(ct);
    }
}
