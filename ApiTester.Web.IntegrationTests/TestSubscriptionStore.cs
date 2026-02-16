using System.Collections.Concurrent;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Entities;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.Web.IntegrationTests;

internal sealed class TestSubscriptionStore : ISubscriptionStore
{
    private sealed record UsageState(
        DateTime PeriodStartUtc,
        DateTime PeriodEndUtc,
        int ProjectsUsed,
        int RunsUsed,
        int AiCallsUsed);

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, UsageState>> UsageByDatabase = new();
    private readonly ConcurrentDictionary<Guid, UsageState> _usageByOrg;
    private readonly ApiTesterDbContext _db;
    private readonly TimeProvider _timeProvider;

    public TestSubscriptionStore(ApiTesterDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _timeProvider = timeProvider;

        var databaseKey = _db.Database.GetConnectionString() ?? _db.Database.ProviderName ?? "in-memory";
        _usageByOrg = UsageByDatabase.GetOrAdd(databaseKey, static _ => new ConcurrentDictionary<Guid, UsageState>());
    }

    public async Task<SubscriptionRecord> GetOrCreateAsync(Guid organisationId, DateTime nowUtc, CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));

        var plan = await ResolvePlanAsync(organisationId, ct);
        var state = NormalizeState(organisationId, nowUtc);

        return new SubscriptionRecord(
            organisationId,
            plan,
            SubscriptionStatus.Active,
            true,
            state.PeriodStartUtc,
            state.PeriodEndUtc,
            state.ProjectsUsed,
            state.RunsUsed,
            state.AiCallsUsed,
            nowUtc);
    }

    public async Task<SubscriptionRecord?> TryConsumeAsync(
        Guid organisationId,
        SubscriptionUsageUpdate update,
        SubscriptionUsageLimits limits,
        DateTime nowUtc,
        CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));

        var plan = await ResolvePlanAsync(organisationId, ct);
        var state = NormalizeState(organisationId, nowUtc);

        if (limits.MaxProjects.HasValue && update.ProjectsDelta > 0
            && state.ProjectsUsed + update.ProjectsDelta > limits.MaxProjects.Value)
            return null;

        if (limits.MaxRunsPerPeriod.HasValue && update.RunsDelta > 0
            && state.RunsUsed + update.RunsDelta > limits.MaxRunsPerPeriod.Value)
            return null;

        if (limits.MaxAiCallsPerPeriod.HasValue && update.AiCallsDelta > 0
            && state.AiCallsUsed + update.AiCallsDelta > limits.MaxAiCallsPerPeriod.Value)
            return null;

        var updated = state with
        {
            ProjectsUsed = state.ProjectsUsed + update.ProjectsDelta,
            RunsUsed = state.RunsUsed + update.RunsDelta,
            AiCallsUsed = state.AiCallsUsed + update.AiCallsDelta
        };

        _usageByOrg[organisationId] = updated;

        return new SubscriptionRecord(
            organisationId,
            plan,
            SubscriptionStatus.Active,
            true,
            updated.PeriodStartUtc,
            updated.PeriodEndUtc,
            updated.ProjectsUsed,
            updated.RunsUsed,
            updated.AiCallsUsed,
            nowUtc);
    }

    public async Task<SubscriptionRecord?> UpdateProjectsUsedAsync(
        Guid organisationId,
        int projectsUsed,
        DateTime nowUtc,
        CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));

        var plan = await ResolvePlanAsync(organisationId, ct);
        var state = NormalizeState(organisationId, nowUtc);

        var updated = state with { ProjectsUsed = projectsUsed };
        _usageByOrg[organisationId] = updated;

        return new SubscriptionRecord(
            organisationId,
            plan,
            SubscriptionStatus.Active,
            true,
            updated.PeriodStartUtc,
            updated.PeriodEndUtc,
            updated.ProjectsUsed,
            updated.RunsUsed,
            updated.AiCallsUsed,
            nowUtc);
    }

    private async Task<SubscriptionPlan> ResolvePlanAsync(Guid organisationId, CancellationToken ct)
    {
        var org = await _db.Organisations.AsNoTracking()
            .FirstOrDefaultAsync(o => o.OrganisationId == organisationId, ct);

        if (org?.OrgSettingsJson is null)
            return SubscriptionPlan.Free;

        var settings = JsonSerializer.Deserialize<OrgSettings>(org.OrgSettingsJson);
        return settings?.Plan switch
        {
            OrgPlan.Pro => SubscriptionPlan.Pro,
            OrgPlan.Team => SubscriptionPlan.Team,
            _ => SubscriptionPlan.Free
        };
    }

    private UsageState NormalizeState(Guid organisationId, DateTime nowUtc)
    {
        var current = _usageByOrg.GetOrAdd(organisationId, _ => CreateState(nowUtc));
        if (nowUtc < current.PeriodEndUtc)
            return current;

        var reset = CreateState(nowUtc);
        _usageByOrg[organisationId] = reset;
        return reset;
    }

    private UsageState CreateState(DateTime nowUtc)
    {
        var periodStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return new UsageState(
            periodStart,
            periodStart.AddMonths(1),
            0,
            0,
            0);
    }
}
