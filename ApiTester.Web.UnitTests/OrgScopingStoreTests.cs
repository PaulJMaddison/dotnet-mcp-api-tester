using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class OrgScopingStoreTests
{
    [Fact]
    public async Task ProjectStore_ListAsync_FiltersByOrganisation()
    {
        var tempDir = CreateTempDir();
        try
        {
            var store = new FileProjectStore(new AppConfig { WorkingDirectory = tempDir });
            var orgA = Guid.NewGuid();
            var orgB = Guid.NewGuid();

            await store.CreateAsync(orgA, "owner-a", "Project A", CancellationToken.None);
            await store.CreateAsync(orgB, "owner-b", "Project B", CancellationToken.None);

            var result = await store.ListAsync(orgA, new PageRequest(10, 0), SortField.CreatedUtc, SortDirection.Desc, CancellationToken.None);

            Assert.Single(result.Items);
            Assert.Equal(orgA, result.Items[0].OrganisationId);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    [Fact]
    public async Task TestRunStore_GetAsync_RequiresOrgMatch()
    {
        var tempDir = CreateTempDir();
        try
        {
            var appConfig = new AppConfig { WorkingDirectory = tempDir };
            var orgStore = new FileOrganisationStore(appConfig);
            var redactionService = new RedactionService();
            var store = new FileTestRunStore(appConfig, NullLogger<FileTestRunStore>.Instance, orgStore, redactionService);
            var orgA = Guid.NewGuid();
            var orgB = Guid.NewGuid();

            var runA = BuildRun(orgA, "proj");
            var runB = BuildRun(orgB, "proj");

            await store.SaveAsync(runA);
            await store.SaveAsync(runB);

            var missing = await store.GetAsync(orgA, runB.RunId);

            Assert.Null(missing);
        }
        finally
        {
            DeleteTempDir(tempDir);
        }
    }

    private static TestRunRecord BuildRun(Guid organisationId, string projectKey)
        => new()
        {
            RunId = Guid.NewGuid(),
            OrganisationId = organisationId,
            OwnerKey = OwnerKeyDefaults.Default,
            ProjectKey = projectKey,
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

    private static string CreateTempDir()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"apitester-orgscope-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        return tempDir;
    }

    private static void DeleteTempDir(string tempDir)
    {
        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);
    }
}
