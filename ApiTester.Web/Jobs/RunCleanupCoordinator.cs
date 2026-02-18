using ApiTester.McpServer.Persistence.Stores;
using ApiTester.Web.Billing;
using Microsoft.Extensions.Options;

namespace ApiTester.Web.Jobs;

public sealed record RetentionCleanupSummary(int OrganisationsProcessed, int RunsPruned);
public sealed record ResponseSnippetCleanupSummary(int OrganisationsProcessed, int ResultsTrimmed, int MaxSnippetLength);

public sealed class RunCleanupCoordinator
{
    private readonly IOrganisationStore _organisationStore;
    private readonly ITestRunStore _testRunStore;
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly TimeProvider _timeProvider;
    private readonly IOptions<CleanupJobOptions> _options;
    private readonly ILogger<RunCleanupCoordinator> _logger;

    public RunCleanupCoordinator(
        IOrganisationStore organisationStore,
        ITestRunStore testRunStore,
        ISubscriptionStore subscriptionStore,
        TimeProvider timeProvider,
        IOptions<CleanupJobOptions> options,
        ILogger<RunCleanupCoordinator> logger)
    {
        _organisationStore = organisationStore;
        _testRunStore = testRunStore;
        _subscriptionStore = subscriptionStore;
        _timeProvider = timeProvider;
        _options = options;
        _logger = logger;
    }

    public async Task<RetentionCleanupSummary> PruneByRetentionPlanAsync(CancellationToken ct)
    {
        var organisations = await _organisationStore.ListAsync(ct);
        var nowUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var totalPruned = 0;

        foreach (var organisation in organisations)
        {
            ct.ThrowIfCancellationRequested();
            var subscription = await _subscriptionStore.GetOrCreateAsync(organisation.OrganisationId, nowUtc, ct);
            var retentionDays = PlanCatalog.GetLimits(subscription.Plan).RetentionDays;
            var cutoffUtc = _timeProvider.GetUtcNow().AddDays(-retentionDays);

            var pruned = await _testRunStore.PruneAsync(organisation.OrganisationId, cutoffUtc, ct);
            totalPruned += pruned;

            if (pruned > 0)
            {
                _logger.LogInformation(
                    "Retention cleanup pruned {PrunedCount} runs for org {OrganisationId} plan {Plan} using {RetentionDays} day window.",
                    pruned,
                    organisation.OrganisationId,
                    subscription.Plan,
                    retentionDays);
            }
        }

        _logger.LogInformation(
            "Retention cleanup completed: processed {OrganisationCount} organisations, pruned {RunsPruned} runs.",
            organisations.Count,
            totalPruned);

        return new RetentionCleanupSummary(organisations.Count, totalPruned);
    }

    public async Task<ResponseSnippetCleanupSummary> TrimLargeResponseSnippetsAsync(CancellationToken ct)
    {
        var maxSnippetLength = Math.Max(1, _options.Value.ResponseSnippetMaxChars);
        var organisations = await _organisationStore.ListAsync(ct);
        var totalTrimmed = 0;

        foreach (var organisation in organisations)
        {
            ct.ThrowIfCancellationRequested();
            var trimmed = await _testRunStore.TrimResponseSnippetsAsync(organisation.OrganisationId, maxSnippetLength, ct);
            totalTrimmed += trimmed;

            if (trimmed > 0)
            {
                _logger.LogInformation(
                    "Response snippet cleanup trimmed {TrimmedCount} entries for org {OrganisationId} to max {MaxSnippetLength} chars.",
                    trimmed,
                    organisation.OrganisationId,
                    maxSnippetLength);
            }
        }

        _logger.LogInformation(
            "Response snippet cleanup completed: processed {OrganisationCount} organisations, trimmed {TrimmedCount} snippets to max {MaxSnippetLength} chars.",
            organisations.Count,
            totalTrimmed,
            maxSnippetLength);

        return new ResponseSnippetCleanupSummary(organisations.Count, totalTrimmed, maxSnippetLength);
    }
}
