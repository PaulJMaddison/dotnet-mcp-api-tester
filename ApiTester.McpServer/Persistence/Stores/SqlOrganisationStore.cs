using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class SqlOrganisationStore : IOrganisationStore
{
    private readonly ApiTesterDbContext _db;

    public SqlOrganisationStore(ApiTesterDbContext db)
    {
        _db = db;
    }

    public async Task<OrganisationRecord> CreateAsync(string name, string slug, CancellationToken ct)
    {
        name = (name ?? string.Empty).Trim();
        slug = (slug ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Organisation name is required.", nameof(name));
        if (string.IsNullOrWhiteSpace(slug))
            throw new ArgumentException("Organisation slug is required.", nameof(slug));

        var existing = await _db.Organisations.AsNoTracking()
            .Where(o => o.Slug == slug)
            .Select(o => new OrganisationRecord(
                o.OrganisationId,
                o.Name,
                o.Slug,
                o.CreatedUtc,
                o.RetentionDays,
                DeserializeRedactionRules(o.RedactionRulesJson)))
            .FirstOrDefaultAsync(ct);
        if (existing is not null)
            return existing;

        var entity = new OrganisationEntity
        {
            OrganisationId = Guid.NewGuid(),
            Name = name,
            Slug = slug,
            CreatedUtc = DateTime.UtcNow,
            RedactionRulesJson = SerializeRedactionRules(Array.Empty<string>())
        };

        _db.Organisations.Add(entity);
        await _db.SaveChangesAsync(ct);
        return new OrganisationRecord(
            entity.OrganisationId,
            entity.Name,
            entity.Slug,
            entity.CreatedUtc,
            entity.RetentionDays,
            DeserializeRedactionRules(entity.RedactionRulesJson));
    }

    public async Task<OrganisationRecord?> GetAsync(Guid organisationId, CancellationToken ct)
    {
        return await _db.Organisations.AsNoTracking()
            .Where(o => o.OrganisationId == organisationId)
            .Select(o => new OrganisationRecord(
                o.OrganisationId,
                o.Name,
                o.Slug,
                o.CreatedUtc,
                o.RetentionDays,
                DeserializeRedactionRules(o.RedactionRulesJson)))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<OrganisationRecord?> GetBySlugAsync(string slug, CancellationToken ct)
    {
        slug = (slug ?? string.Empty).Trim();
        return await _db.Organisations.AsNoTracking()
            .Where(o => o.Slug == slug)
            .Select(o => new OrganisationRecord(
                o.OrganisationId,
                o.Name,
                o.Slug,
                o.CreatedUtc,
                o.RetentionDays,
                DeserializeRedactionRules(o.RedactionRulesJson)))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<OrganisationRecord?> UpdateSettingsAsync(
        Guid organisationId,
        int? retentionDays,
        IReadOnlyList<string> redactionRules,
        CancellationToken ct)
    {
        var entity = await _db.Organisations.FirstOrDefaultAsync(o => o.OrganisationId == organisationId, ct);
        if (entity is null)
            return null;

        entity.RetentionDays = retentionDays;
        entity.RedactionRulesJson = SerializeRedactionRules(redactionRules);
        await _db.SaveChangesAsync(ct);

        return new OrganisationRecord(
            entity.OrganisationId,
            entity.Name,
            entity.Slug,
            entity.CreatedUtc,
            entity.RetentionDays,
            DeserializeRedactionRules(entity.RedactionRulesJson));
    }

    private static List<string> DeserializeRedactionRules(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new List<string>();

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
        }
        catch (System.Text.Json.JsonException)
        {
            return new List<string>();
        }
    }

    private static string SerializeRedactionRules(IReadOnlyList<string> rules)
    {
        var normalized = rules
            .Where(r => !string.IsNullOrWhiteSpace(r))
            .Select(r => r.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return System.Text.Json.JsonSerializer.Serialize(normalized);
    }
}
