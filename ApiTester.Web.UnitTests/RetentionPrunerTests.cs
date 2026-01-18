using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class RetentionPrunerTests
{
    [Fact]
    public async Task PruneAsync_RemovesOldRuns_KeepingAuditEvents()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"apitester-retention-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var appConfig = new AppConfig { WorkingDirectory = tempDir };
            var orgStore = new FileOrganisationStore(appConfig);
            var redactionService = new RedactionService();
            var runStore = new FileTestRunStore(appConfig, NullLogger<FileTestRunStore>.Instance, orgStore, redactionService);
            var auditStore = new FileAuditEventStore(appConfig);

            var org = await orgStore.CreateAsync("Retention Org", "retention-org", CancellationToken.None);
            await orgStore.UpdateSettingsAsync(org.OrganisationId, 1, Array.Empty<string>(), CancellationToken.None);

            var fixedNow = new DateTimeOffset(2024, 3, 1, 12, 0, 0, TimeSpan.Zero);
            var pruner = new RetentionPruner(
                orgStore,
                runStore,
                new FixedTimeProvider(fixedNow),
                NullLogger<RetentionPruner>.Instance);

            var oldRun = BuildRun(org.OrganisationId, fixedNow.AddDays(-2));
            var recentRun = BuildRun(org.OrganisationId, fixedNow.AddHours(-2));

            await runStore.SaveAsync(oldRun);
            await runStore.SaveAsync(recentRun);

            await auditStore.CreateAsync(new AuditEventRecord(
                Guid.NewGuid(),
                org.OrganisationId,
                Guid.NewGuid(),
                AuditActions.RunExecuted,
                "run",
                oldRun.RunId.ToString(),
                fixedNow.UtcDateTime,
                "{}"), CancellationToken.None);

            var result = await pruner.PruneAsync(org.OrganisationId, CancellationToken.None);

            Assert.Equal(1, result.RunsPruned);
            Assert.Null(await runStore.GetAsync(org.OrganisationId, oldRun.RunId));
            Assert.NotNull(await runStore.GetAsync(org.OrganisationId, recentRun.RunId));

            var audits = await auditStore.ListAsync(org.OrganisationId, 10, null, null, null, CancellationToken.None);
            Assert.Single(audits);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }

    private static TestRunRecord BuildRun(Guid organisationId, DateTimeOffset completedUtc)
        => new()
        {
            RunId = Guid.NewGuid(),
            OrganisationId = organisationId,
            OperationId = "op",
            StartedUtc = completedUtc.AddMinutes(-1),
            CompletedUtc = completedUtc,
            Result = new TestRunResult
            {
                OperationId = "op",
                TotalCases = 1,
                Passed = 1,
                Failed = 0,
                Blocked = 0,
                TotalDurationMs = 5,
                Results = new List<TestCaseResult>
                {
                    new()
                    {
                        Name = "Happy",
                        Pass = true,
                        StatusCode = 200,
                        ResponseSnippet = "ok"
                    }
                }
            }
        };

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
