using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class RunAuditImmutabilityTests
{
    [Fact]
    public async Task SetBaselineAsync_DoesNotOverwriteAuditMetadata()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var appConfig = new AppConfig { WorkingDirectory = tempDir };
            var orgStore = new FileOrganisationStore(appConfig);
            var redactionService = new RedactionService();
            var store = new FileTestRunStore(
                appConfig,
                NullLogger<FileTestRunStore>.Instance,
                orgStore,
                redactionService);

            var baseline = BuildRunRecord("baseline-actor", "baseline-env");
            var run = BuildRunRecord("run-actor", "run-env");

            await store.SaveAsync(baseline);
            await store.SaveAsync(run);

            var updated = await store.SetBaselineAsync(run.OrganisationId, run.RunId, baseline.RunId);
            Assert.True(updated);

            var reloaded = await store.GetAsync(run.OrganisationId, run.RunId);

            Assert.NotNull(reloaded);
            Assert.Equal(run.Actor, reloaded!.Actor);
            Assert.Equal(run.Environment?.Name, reloaded.Environment?.Name);
            Assert.Equal(run.Environment?.BaseUrl, reloaded.Environment?.BaseUrl);
            Assert.NotNull(reloaded.PolicySnapshot);
            Assert.Equal(run.PolicySnapshot!.DryRun, reloaded.PolicySnapshot!.DryRun);
            Assert.Equal(run.PolicySnapshot.AllowedBaseUrls, reloaded.PolicySnapshot.AllowedBaseUrls);
            Assert.Equal(run.PolicySnapshot.AllowedMethods, reloaded.PolicySnapshot.AllowedMethods);
            Assert.Equal(run.PolicySnapshot.BlockLocalhost, reloaded.PolicySnapshot.BlockLocalhost);
            Assert.Equal(run.PolicySnapshot.BlockPrivateNetworks, reloaded.PolicySnapshot.BlockPrivateNetworks);
            Assert.Equal(run.PolicySnapshot.TimeoutSeconds, reloaded.PolicySnapshot.TimeoutSeconds);
            Assert.Equal(run.PolicySnapshot.MaxRequestBodyBytes, reloaded.PolicySnapshot.MaxRequestBodyBytes);
            Assert.Equal(run.PolicySnapshot.MaxResponseBodyBytes, reloaded.PolicySnapshot.MaxResponseBodyBytes);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private static TestRunRecord BuildRunRecord(string actor, string environmentName)
        => new()
        {
            RunId = Guid.NewGuid(),
            OrganisationId = OrgDefaults.DefaultOrganisationId,
            Actor = actor,
            Environment = new TestRunEnvironmentSnapshot(environmentName, "https://example.com"),
            PolicySnapshot = new ApiExecutionPolicySnapshot(
                DryRun: true,
                AllowedBaseUrls: new List<string> { "https://example.com" },
                AllowedMethods: new List<string> { "GET", "POST" },
                BlockLocalhost: true,
                BlockPrivateNetworks: true,
                TimeoutSeconds: 30,
                MaxRequestBodyBytes: 1024,
                MaxResponseBodyBytes: 2048),
            OwnerKey = "owner",
            ProjectKey = "default",
            OperationId = "op",
            StartedUtc = DateTimeOffset.UtcNow,
            CompletedUtc = DateTimeOffset.UtcNow,
            Result = new TestRunResult
            {
                OperationId = "op",
                TotalCases = 0,
                Passed = 0,
                Failed = 0,
                Blocked = 0,
                TotalDurationMs = 0,
                Results = new List<TestCaseResult>()
            }
        };
}
