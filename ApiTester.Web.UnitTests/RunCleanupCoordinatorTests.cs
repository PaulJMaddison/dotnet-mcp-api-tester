using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.Web.Jobs;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class RunCleanupCoordinatorTests
{
    [Fact]
    public async Task PruneByRetentionPlanAsync_UsesPlanRetentionPerOrganisation()
    {
        var now = new DateTimeOffset(2026, 2, 18, 10, 0, 0, TimeSpan.Zero);
        var orgA = new OrganisationRecord(Guid.NewGuid(), "A", "a", now.UtcDateTime);
        var orgB = new OrganisationRecord(Guid.NewGuid(), "B", "b", now.UtcDateTime);

        var orgStore = new FakeOrganisationStore([orgA, orgB]);
        var runStore = new FakeRunStore();
        var subscriptionStore = new FakeSubscriptionStore(new Dictionary<Guid, SubscriptionPlan>
        {
            [orgA.OrganisationId] = SubscriptionPlan.Free,
            [orgB.OrganisationId] = SubscriptionPlan.Team
        });

        var coordinator = new RunCleanupCoordinator(
            orgStore,
            runStore,
            subscriptionStore,
            new FixedTimeProvider(now),
            Options.Create(new CleanupJobOptions()),
            NullLogger<RunCleanupCoordinator>.Instance);

        var summary = await coordinator.PruneByRetentionPlanAsync(CancellationToken.None);

        Assert.Equal(2, summary.OrganisationsProcessed);
        Assert.Equal(2, summary.RunsPruned);
        Assert.Equal(2, runStore.PruneCalls.Count);

        var freeCutoff = runStore.PruneCalls.Single(c => c.TenantId == orgA.OrganisationId).CutoffUtc;
        var teamCutoff = runStore.PruneCalls.Single(c => c.TenantId == orgB.OrganisationId).CutoffUtc;

        Assert.Equal(now.AddDays(-7), freeCutoff);
        Assert.Equal(now.AddDays(-90), teamCutoff);
    }

    [Fact]
    public async Task TrimLargeResponseSnippetsAsync_UsesConfiguredMaxLength()
    {
        var now = new DateTimeOffset(2026, 2, 18, 10, 0, 0, TimeSpan.Zero);
        var orgA = new OrganisationRecord(Guid.NewGuid(), "A", "a", now.UtcDateTime);
        var orgB = new OrganisationRecord(Guid.NewGuid(), "B", "b", now.UtcDateTime);

        var orgStore = new FakeOrganisationStore([orgA, orgB]);
        var runStore = new FakeRunStore();
        runStore.TrimResultByOrg[orgA.OrganisationId] = 2;
        runStore.TrimResultByOrg[orgB.OrganisationId] = 1;

        var coordinator = new RunCleanupCoordinator(
            orgStore,
            runStore,
            new FakeSubscriptionStore(new Dictionary<Guid, SubscriptionPlan>()),
            new FixedTimeProvider(now),
            Options.Create(new CleanupJobOptions { ResponseSnippetMaxChars = 128 }),
            NullLogger<RunCleanupCoordinator>.Instance);

        var summary = await coordinator.TrimLargeResponseSnippetsAsync(CancellationToken.None);

        Assert.Equal(2, summary.OrganisationsProcessed);
        Assert.Equal(3, summary.ResultsTrimmed);
        Assert.Equal(128, summary.MaxSnippetLength);
        Assert.Equal(2, runStore.TrimCalls.Count);
        Assert.All(runStore.TrimCalls, c => Assert.Equal(128, c.MaxLength));
    }

    private sealed class FakeOrganisationStore : IOrganisationStore
    {
        private readonly IReadOnlyList<OrganisationRecord> _organisations;

        public FakeOrganisationStore(IReadOnlyList<OrganisationRecord> organisations)
        {
            _organisations = organisations;
        }

        public Task<OrganisationRecord> CreateAsync(string name, string slug, CancellationToken ct) => throw new NotSupportedException();

        public Task<OrganisationRecord?> GetAsync(Guid organisationId, CancellationToken ct)
            => Task.FromResult(_organisations.FirstOrDefault(o => o.OrganisationId == organisationId));

        public Task<OrganisationRecord?> GetBySlugAsync(string slug, CancellationToken ct)
            => Task.FromResult(_organisations.FirstOrDefault(o => o.Slug == slug));

        public Task<IReadOnlyList<OrganisationRecord>> ListAsync(CancellationToken ct)
            => Task.FromResult(_organisations);

        public Task<OrganisationRecord?> UpdateSettingsAsync(Guid organisationId, int? retentionDays, IReadOnlyList<string> redactionRules, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class FakeRunStore : ITestRunStore
    {
        public List<(Guid TenantId, DateTimeOffset CutoffUtc)> PruneCalls { get; } = new();
        public List<(Guid TenantId, int MaxLength)> TrimCalls { get; } = new();
        public Dictionary<Guid, int> TrimResultByOrg { get; } = new();

        public Task SaveAsync(TestRunRecord record) => throw new NotSupportedException();
        public Task<TestRunRecord?> GetAsync(Guid tenantId, Guid runId) => throw new NotSupportedException();
        public Task<bool> SetBaselineAsync(Guid tenantId, Guid runId, Guid baselineRunId) => throw new NotSupportedException();
        public Task<PagedResult<TestRunRecord>> ListAsync(Guid tenantId, string projectKey, PageRequest request, SortField sortField, SortDirection direction, string? operationId = null, DateTimeOffset? notBeforeUtc = null) => throw new NotSupportedException();

        public Task<int> PruneAsync(Guid tenantId, DateTimeOffset cutoffUtc, CancellationToken ct)
        {
            PruneCalls.Add((tenantId, cutoffUtc));
            return Task.FromResult(1);
        }

        public Task<int> TrimResponseSnippetsAsync(Guid tenantId, int maxSnippetLength, CancellationToken ct)
        {
            TrimCalls.Add((tenantId, maxSnippetLength));
            return Task.FromResult(TrimResultByOrg.TryGetValue(tenantId, out var count) ? count : 0);
        }
    }

    private sealed class FakeSubscriptionStore : ISubscriptionStore
    {
        private readonly IReadOnlyDictionary<Guid, SubscriptionPlan> _plans;

        public FakeSubscriptionStore(IReadOnlyDictionary<Guid, SubscriptionPlan> plans)
        {
            _plans = plans;
        }

        public Task<SubscriptionRecord> GetOrCreateAsync(Guid organisationId, DateTime nowUtc, CancellationToken ct)
        {
            var plan = _plans.TryGetValue(organisationId, out var value) ? value : SubscriptionPlan.Free;
            return Task.FromResult(new SubscriptionRecord(
                organisationId,
                organisationId,
                plan,
                SubscriptionStatus.Active,
                true,
                null,
                null,
                nowUtc.AddDays(-1),
                nowUtc.AddDays(30),
                nowUtc));
        }

        public Task<SubscriptionRecord> UpsertStripeAsync(Guid organisationId, Guid tenantId, SubscriptionPlan plan, SubscriptionStatus status, bool renews, string? stripeCustomerId, string? stripeSubscriptionId, DateTime periodStartUtc, DateTime periodEndUtc, DateTime nowUtc, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<UsageCounterRecord> GetOrCreateUsageAsync(Guid tenantId, DateTime nowUtc, CancellationToken ct)
            => Task.FromResult(new UsageCounterRecord(tenantId, nowUtc.AddDays(-1), nowUtc.AddDays(30), 0, 0, 0, 0, nowUtc));

        public Task<UsageCounterRecord?> TryConsumeAsync(Guid organisationId, SubscriptionUsageUpdate update, SubscriptionUsageLimits limits, DateTime nowUtc, CancellationToken ct)
            => throw new NotSupportedException();

        public Task<UsageCounterRecord?> UpdateProjectsUsedAsync(Guid organisationId, int projectsUsed, DateTime nowUtc, CancellationToken ct)
            => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _utcNow;

        public FixedTimeProvider(DateTimeOffset utcNow)
        {
            _utcNow = utcNow;
        }

        public override DateTimeOffset GetUtcNow() => _utcNow;
    }
}
