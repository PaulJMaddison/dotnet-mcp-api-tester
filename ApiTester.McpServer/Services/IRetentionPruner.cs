namespace ApiTester.McpServer.Services;

public interface IRetentionPruner
{
    Task<RetentionPruneResult> PruneAsync(Guid organisationId, CancellationToken ct);
}

public sealed record RetentionPruneResult(
    Guid OrganisationId,
    int? RetentionDays,
    DateTimeOffset? CutoffUtc,
    int RunsPruned);
