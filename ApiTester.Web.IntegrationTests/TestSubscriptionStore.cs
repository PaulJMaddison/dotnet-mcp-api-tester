using System.Collections.Concurrent;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.EntityFrameworkCore;

namespace ApiTester.Web.IntegrationTests;

internal sealed class TestSubscriptionStore : ISubscriptionStore
{
    private sealed record UsageState(DateTime PeriodStartUtc, DateTime PeriodEndUtc, int ProjectsUsed, int RunsUsed, int AiCallsUsed, int ExportsUsed);

    private static readonly ConcurrentDictionary<string, ConcurrentDictionary<Guid, UsageState>> UsageByDatabase = new();
    private readonly ConcurrentDictionary<Guid, UsageState> _usageByOrg;
    private readonly ApiTesterDbContext _db;

    public TestSubscriptionStore(ApiTesterDbContext db)
    {
        _db = db;
        var databaseKey = _db.Database.GetConnectionString() ?? _db.Database.ProviderName ?? "in-memory";
        _usageByOrg = UsageByDatabase.GetOrAdd(databaseKey, static _ => new ConcurrentDictionary<Guid, UsageState>());
    }

    public async Task<SubscriptionRecord> GetOrCreateAsync(Guid organisationId, DateTime nowUtc, CancellationToken ct)
        => new(organisationId, organisationId, await ResolvePlanAsync(organisationId, ct), SubscriptionStatus.Active, true, null, null, CreateState(nowUtc).PeriodStartUtc, CreateState(nowUtc).PeriodEndUtc, nowUtc);

    public async Task<SubscriptionRecord> UpsertStripeAsync(Guid organisationId, Guid tenantId, SubscriptionPlan plan, SubscriptionStatus status, bool renews, string? stripeCustomerId, string? stripeSubscriptionId, DateTime periodStartUtc, DateTime periodEndUtc, DateTime nowUtc, CancellationToken ct)
        => new(organisationId, tenantId, plan, status, renews, stripeCustomerId, stripeSubscriptionId, periodStartUtc, periodEndUtc, nowUtc);

    public Task<UsageCounterRecord> GetOrCreateUsageAsync(Guid tenantId, DateTime nowUtc, CancellationToken ct)
    {
        var state = NormalizeState(tenantId, nowUtc);
        return Task.FromResult(new UsageCounterRecord(tenantId, state.PeriodStartUtc, state.PeriodEndUtc, state.ProjectsUsed, state.RunsUsed, state.AiCallsUsed, state.ExportsUsed, nowUtc));
    }

    public async Task<UsageCounterRecord?> TryConsumeAsync(Guid organisationId, SubscriptionUsageUpdate update, SubscriptionUsageLimits limits, DateTime nowUtc, CancellationToken ct)
    {
        _ = await ResolvePlanAsync(organisationId, ct);
        var state = NormalizeState(organisationId, nowUtc);

        if (limits.MaxProjects.HasValue && update.ProjectsDelta > 0 && state.ProjectsUsed + update.ProjectsDelta > limits.MaxProjects.Value) return null;
        if (limits.MaxRunsPerPeriod.HasValue && update.RunsDelta > 0 && state.RunsUsed + update.RunsDelta > limits.MaxRunsPerPeriod.Value) return null;
        if (limits.MaxAiCallsPerPeriod.HasValue && update.AiCallsDelta > 0 && state.AiCallsUsed + update.AiCallsDelta > limits.MaxAiCallsPerPeriod.Value) return null;
        if (limits.MaxExportsPerPeriod.HasValue && update.ExportsDelta > 0 && state.ExportsUsed + update.ExportsDelta > limits.MaxExportsPerPeriod.Value) return null;

        var updated = state with
        {
            ProjectsUsed = state.ProjectsUsed + update.ProjectsDelta,
            RunsUsed = state.RunsUsed + update.RunsDelta,
            AiCallsUsed = state.AiCallsUsed + update.AiCallsDelta,
            ExportsUsed = state.ExportsUsed + update.ExportsDelta
        };

        _usageByOrg[organisationId] = updated;
        return new UsageCounterRecord(organisationId, updated.PeriodStartUtc, updated.PeriodEndUtc, updated.ProjectsUsed, updated.RunsUsed, updated.AiCallsUsed, updated.ExportsUsed, nowUtc);
    }

    public Task<UsageCounterRecord?> UpdateProjectsUsedAsync(Guid organisationId, int projectsUsed, DateTime nowUtc, CancellationToken ct)
    {
        var state = NormalizeState(organisationId, nowUtc);
        var updated = state with { ProjectsUsed = projectsUsed };
        _usageByOrg[organisationId] = updated;
        return Task.FromResult<UsageCounterRecord?>(new UsageCounterRecord(organisationId, updated.PeriodStartUtc, updated.PeriodEndUtc, updated.ProjectsUsed, updated.RunsUsed, updated.AiCallsUsed, updated.ExportsUsed, nowUtc));
    }

    private async Task<SubscriptionPlan> ResolvePlanAsync(Guid organisationId, CancellationToken ct)
    {
        var org = await _db.Organisations.AsNoTracking().FirstOrDefaultAsync(o => o.OrganisationId == organisationId, ct);
        if (org?.OrgSettingsJson is null) return SubscriptionPlan.Free;
        var settings = JsonSerializer.Deserialize<OrgSettings>(org.OrgSettingsJson);
        return settings?.Plan switch { OrgPlan.Pro => SubscriptionPlan.Pro, OrgPlan.Team => SubscriptionPlan.Team, _ => SubscriptionPlan.Free };
    }

    private UsageState NormalizeState(Guid organisationId, DateTime nowUtc)
    {
        var current = _usageByOrg.GetOrAdd(organisationId, _ => CreateState(nowUtc));
        if (nowUtc < current.PeriodEndUtc) return current;
        var reset = CreateState(nowUtc);
        _usageByOrg[organisationId] = reset;
        return reset;
    }

    private static UsageState CreateState(DateTime nowUtc)
    {
        var periodStart = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        return new UsageState(periodStart, periodStart.AddMonths(1), 0, 0, 0, 0);
    }
}
