using ApiTester.McpServer.Models;
using ApiTester.McpServer.Services;

namespace ApiTester.Web.Reports;

public static class RunExportRedactor
{
    public static TestRunRecord RedactRun(TestRunRecord run, RedactionService redactionService, IReadOnlyList<string>? patterns)
    {
        var redactedResult = redactionService.RedactResult(run.Result, patterns);
        var redactedEnvironment = run.Environment is null
            ? null
            : new TestRunEnvironmentSnapshot(
                run.Environment.Name,
                redactionService.RedactText(run.Environment.BaseUrl, patterns));

        var redactedPolicy = run.PolicySnapshot is null
            ? null
            : new ApiExecutionPolicySnapshot(
                run.PolicySnapshot.HostedMode,
                run.PolicySnapshot.DryRun,
                run.PolicySnapshot.AllowedBaseUrls
                    .Select(url => redactionService.RedactText(url, patterns) ?? url)
                    .ToList(),
                run.PolicySnapshot.AllowedMethods,
                run.PolicySnapshot.BlockLocalhost,
                run.PolicySnapshot.BlockPrivateNetworks,
                run.PolicySnapshot.TimeoutSeconds,
                run.PolicySnapshot.MaxRequestBodyBytes,
                run.PolicySnapshot.MaxResponseBodyBytes,
                run.PolicySnapshot.ValidateSchema,
                run.PolicySnapshot.RetryOnFlake,
                run.PolicySnapshot.MaxRetries);

        return new TestRunRecord
        {
            RunId = run.RunId,
            OrganisationId = run.OrganisationId,
            Actor = run.Actor,
            Environment = redactedEnvironment,
            PolicySnapshot = redactedPolicy,
            OwnerKey = run.OwnerKey,
            OperationId = run.OperationId,
            SpecId = run.SpecId,
            BaselineRunId = run.BaselineRunId,
            StartedUtc = run.StartedUtc,
            CompletedUtc = run.CompletedUtc,
            Result = redactedResult,
            ProjectKey = run.ProjectKey
        };
    }
}
