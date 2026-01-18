using ApiTester.McpServer.Persistence.Stores;
using Microsoft.Extensions.Logging;

namespace ApiTester.McpServer.Services;

public sealed class RetentionPruner : IRetentionPruner
{
    private readonly IOrganisationStore _organisationStore;
    private readonly ITestRunStore _testRunStore;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RetentionPruner> _logger;

    public RetentionPruner(
        IOrganisationStore organisationStore,
        ITestRunStore testRunStore,
        TimeProvider timeProvider,
        ILogger<RetentionPruner> logger)
    {
        _organisationStore = organisationStore;
        _testRunStore = testRunStore;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<RetentionPruneResult> PruneAsync(Guid organisationId, CancellationToken ct)
    {
        if (organisationId == Guid.Empty)
            throw new ArgumentException("Organisation ID is required.", nameof(organisationId));

        var org = await _organisationStore.GetAsync(organisationId, ct);
        if (org is null)
            return new RetentionPruneResult(organisationId, null, null, 0);

        if (!org.RetentionDays.HasValue || org.RetentionDays.Value <= 0)
            return new RetentionPruneResult(organisationId, org.RetentionDays, null, 0);

        var cutoff = _timeProvider.GetUtcNow().AddDays(-org.RetentionDays.Value);
        var pruned = await _testRunStore.PruneAsync(organisationId, cutoff, ct);

        _logger.LogInformation(
            "Pruned {Count} runs for org {OrganisationId} using retention {RetentionDays} days (cutoff {CutoffUtc}).",
            pruned,
            organisationId,
            org.RetentionDays.Value,
            cutoff);

        return new RetentionPruneResult(organisationId, org.RetentionDays, cutoff, pruned);
    }
}
