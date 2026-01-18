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

    [Fact]
    public async Task CompareAsync_MissingRun_ReturnsRunNotFound()
    {
        var run = BuildRun();
        var runStore = new FakeRunStore();
        var baselineStore = new FakeBaselineStore(null);
        var service = new BaselineComparisonService(runStore, baselineStore, new RunComparisonService());

        var result = await service.CompareAsync(run.OrganisationId, run.RunId, CancellationToken.None);

        Assert.Equal(BaselineComparisonStatus.RunNotFound, result.Status);
        Assert.Null(result.Response);
    }

    [Fact]
    public async Task CompareAsync_WithBaselineAndRun_ReturnsComparison()
    {
        var run = BuildRun();
        var baselineRun = BuildRun(
            organisationId: run.OrganisationId,
            projectKey: run.ProjectKey,
            operationId: run.OperationId);

        var baseline = new BaselineRecord(baselineRun.RunId, run.ProjectKey, run.OperationId, DateTimeOffset.UtcNow);
        var runStore = new FakeRunStore(run, baselineRun);
        var baselineStore = new FakeBaselineStore(baseline);
        var service = new BaselineComparisonService(runStore, baselineStore, new RunComparisonService());

        var result = await service.CompareAsync(run.OrganisationId, run.RunId, CancellationToken.None);

        Assert.Equal(BaselineComparisonStatus.Ok, result.Status);
        Assert.NotNull(result.Response);
        Assert.Equal(run.RunId, result.Response?.RunId);
        Assert.Equal(baselineRun.RunId, result.Response?.BaselineRunId);
    }

    private static TestRunRecord BuildRun(
        Guid? organisationId = null,
        string? projectKey = null,
        string? operationId = null,
        Guid? runId = null)
    {
        var resolvedOperationId = operationId ?? "op-1";
        var resolvedProjectKey = projectKey ?? "project";
        return new TestRunRecord
        {
            RunId = runId ?? Guid.NewGuid(),
            OrganisationId = organisationId ?? Guid.NewGuid(),
            ProjectKey = resolvedProjectKey,
            OperationId = resolvedOperationId,
            StartedUtc = DateTimeOffset.UtcNow.AddMinutes(-1),
            CompletedUtc = DateTimeOffset.UtcNow,
            Result = new TestRunResult
            {
                OperationId = resolvedOperationId,
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
        private readonly Dictionary<Guid, TestRunRecord> _runs;

        public FakeRunStore(params TestRunRecord[] runs)
        {
            _runs = runs.ToDictionary(record => record.RunId, record => record);
        }

        public Task SaveAsync(TestRunRecord record) => Task.CompletedTask;

        public Task<TestRunRecord?> GetAsync(Guid organisationId, Guid runId)
            => Task.FromResult(_runs.TryGetValue(runId, out var record) ? record : null);

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
