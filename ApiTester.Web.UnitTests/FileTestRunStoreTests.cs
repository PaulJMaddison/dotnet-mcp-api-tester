using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class FileTestRunStoreTests
{
    [Fact]
    public async Task SaveAsync_NormalizesKeysAndUtc_PreservesResults()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"apitester-runstore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var store = new FileTestRunStore(new AppConfig { WorkingDirectory = tempDir }, NullLogger<FileTestRunStore>.Instance);
            var started = new DateTimeOffset(2024, 1, 1, 12, 0, 0, TimeSpan.FromHours(2));
            var completed = started.AddMinutes(1);

            var record = new TestRunRecord
            {
                RunId = Guid.NewGuid(),
                OwnerKey = "  ",
                ProjectKey = "  ",
                OperationId = "op",
                StartedUtc = started,
                CompletedUtc = completed,
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
                            Blocked = false,
                            Pass = true,
                            StatusCode = 200
                        }
                    }
                }
            };

            await store.SaveAsync(record);

            var loaded = await store.GetAsync(OwnerKeyDefaults.Default, record.RunId);

            Assert.NotNull(loaded);
            Assert.Equal(OwnerKeyDefaults.Default, loaded!.OwnerKey);
            Assert.Equal("default", loaded.ProjectKey);
            Assert.Equal(TimeSpan.Zero, loaded.StartedUtc.Offset);
            Assert.Equal(started.UtcDateTime, loaded.StartedUtc.UtcDateTime);
            Assert.Single(loaded.Result.Results);
            Assert.Equal(1, loaded.Result.ClassificationSummary.Pass);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, true);
        }
    }
}
