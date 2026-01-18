using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.Web.Comparison;
using Xunit;

namespace ApiTester.Web.UnitTests;

public sealed class BaselineComparisonServiceTests
{
    [Fact]
    public async Task CompareAsync_MissingBaseline_ReturnsBaselineNotFound()
    {
        var run = BuildRun();
        var runStore = new FakeRunStore(run);
        var baselineStore = new FakeBaselineStore(null);
        var service = new BaselineComparisonService(runStore, baselineStore, new RunComparisonService());

        var result = await service.CompareAsync(run.OrganisationId, run.RunId, CancellationToken.None);

        Assert.Equal(BaselineComparisonStatus.BaselineNotFound, result.Status);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task CompareAsync_MissingBaselineRun_ReturnsBaselineNotFound()
    {
        var run = BuildRun();
        var baseline = new BaselineRecord(Guid.NewGuid(), run.ProjectKey, run.OperationId, DateTimeOffset.UtcNow);
        var runStore = new FakeRunStore(run);
        var baselineStore = new FakeBaselineStore(baseline);
        var service = new BaselineComparisonService(runStore, baselineStore, new RunComparisonService());

        var result = await service.CompareAsync(run.OrganisationId, run.RunId, CancellationToken.None);

        Assert.Equal(BaselineComparisonStatus.BaselineNotFound, result.Status);
        Assert.Null(result.Response);
    }

    private static TestRunRecord BuildRun()
    {
        return new TestRunRecord
        {
            RunId = Guid.NewGuid(),
            OrganisationId = Guid.NewGuid(),
            ProjectKey = "project",
            OperationId = "op-1",
            StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            CompletedUtc = DateTimeOffset.UtcNow,
            Result = new TestRunResult
            {
                OperationId = "op-1",
                TotalCases = 1,
                Passed = 1,
                Failed = 0,
                Blocked = 0,
                TotalDurationMs = 50,
                Results =
                [
                    new TestCaseResult
                    {
                        Name = "case-a",
                        Method = "GET",
                        Url = "https://example.test",
                        StatusCode = 200,
                        DurationMs = 50,
                        Pass = true,
                        Blocked = false
                    }
                ]
            }
        };
    }

    private sealed class FakeRunStore : ITestRunStore
    {
        private readonly TestRunRecord _run;

        public FakeRunStore(TestRunRecord run)
        {
            _run = run;
        }

        public Task SaveAsync(TestRunRecord record) => Task.CompletedTask;

        public Task<TestRunRecord?> GetAsync(Guid organisationId, Guid runId)
            => Task.FromResult(runId == _run.RunId ? _run : null);

        public Task<bool> SetBaselineAsync(Guid organisationId, Guid runId, Guid baselineRunId)
            => Task.FromResult(false);

        public Task<PagedResult<TestRunRecord>> ListAsync(Guid organisationId, string projectKey, PageRequest request, SortField sortField, SortDirection direction, string? operationId = null)
            => Task.FromResult(new PagedResult<TestRunRecord>(Array.Empty<TestRunRecord>(), 0, null));

        public Task<int> PruneAsync(Guid organisationId, DateTimeOffset cutoffUtc, CancellationToken ct)
            => Task.FromResult(0);
    }

    private sealed class FakeBaselineStore : IBaselineStore
    {
        private readonly BaselineRecord? _baseline;

        public FakeBaselineStore(BaselineRecord? baseline)
        {
            _baseline = baseline;
        }

        public Task<BaselineRecord?> GetAsync(Guid organisationId, string projectKey, string operationId, CancellationToken ct)
            => Task.FromResult(_baseline);

        public Task<IReadOnlyList<BaselineRecord>> ListAsync(Guid organisationId, string? projectKey, string? operationId, int take, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<BaselineRecord>>(Array.Empty<BaselineRecord>());

        public Task<BaselineRecord?> SetAsync(Guid organisationId, string projectKey, string operationId, Guid runId, CancellationToken ct)
            => Task.FromResult<BaselineRecord?>(null);
    }
}
