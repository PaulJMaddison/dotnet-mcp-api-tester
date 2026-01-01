using ApiTester.McpServer.Models;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Mapping;

public static class RunMapping
{
    public static RunSummaryResponse ToSummaryResponse(string projectKey, PageMetadata metadata, IReadOnlyList<TestRunRecord> runs)
    {
        var items = runs.Select(ToSummaryDto).ToList();
        return new RunSummaryResponse(projectKey, items, metadata);
    }

    public static RunSummaryDto ToSummaryDto(TestRunRecord record)
    {
        var summary = new RunSummary(
            record.Result.TotalCases,
            record.Result.Passed,
            record.Result.Failed,
            record.Result.Blocked,
            record.Result.TotalDurationMs,
            record.Result.ClassificationSummary);

        return new RunSummaryDto(
            record.RunId,
            record.ProjectKey,
            record.OperationId,
            record.StartedUtc,
            record.CompletedUtc,
            summary);
    }

    public static RunDetailDto ToDetailDto(TestRunRecord record)
        => new(
            record.RunId,
            record.ProjectKey,
            record.OperationId,
            record.StartedUtc,
            record.CompletedUtc,
            record.Result);

    public static RunAuditResponse ToAuditResponse(TestRunRecord record)
    {
        var environment = record.Environment is null
            ? null
            : new RunEnvironmentSnapshot(record.Environment.Name, record.Environment.BaseUrl);

        var policy = record.PolicySnapshot is null
            ? null
            : new RunPolicySnapshot(
                record.PolicySnapshot.DryRun,
                record.PolicySnapshot.AllowedBaseUrls,
                record.PolicySnapshot.AllowedMethods,
                record.PolicySnapshot.BlockLocalhost,
                record.PolicySnapshot.BlockPrivateNetworks,
                record.PolicySnapshot.TimeoutSeconds,
                record.PolicySnapshot.MaxRequestBodyBytes,
                record.PolicySnapshot.MaxResponseBodyBytes);

        return new RunAuditResponse(
            record.RunId,
            record.Actor,
            environment,
            policy);
    }
}
