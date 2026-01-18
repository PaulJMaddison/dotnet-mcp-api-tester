using ApiTester.McpServer.Persistence.Stores;
using ApiTester.Web.Contracts;

namespace ApiTester.Web.Comparison;

public enum BaselineComparisonStatus
{
    Ok,
    RunNotFound,
    BaselineNotFound
}

public sealed record BaselineComparisonResult(BaselineComparisonStatus Status, RunComparisonResponse? Response);

public sealed class BaselineComparisonService
{
    private readonly ITestRunStore _runStore;
    private readonly IBaselineStore _baselineStore;
    private readonly RunComparisonService _comparison;

    public BaselineComparisonService(
        ITestRunStore runStore,
        IBaselineStore baselineStore,
        RunComparisonService comparison)
    {
        _runStore = runStore;
        _baselineStore = baselineStore;
        _comparison = comparison;
    }

    public async Task<BaselineComparisonResult> CompareAsync(Guid organisationId, Guid runId, CancellationToken ct)
    {
        var run = await _runStore.GetAsync(organisationId, runId);
        if (run is null)
            return new BaselineComparisonResult(BaselineComparisonStatus.RunNotFound, null);

        var baseline = await _baselineStore.GetAsync(organisationId, run.ProjectKey, run.OperationId, ct);
        if (baseline is null)
            return new BaselineComparisonResult(BaselineComparisonStatus.BaselineNotFound, null);

        var baselineRun = await _runStore.GetAsync(organisationId, baseline.RunId);
        if (baselineRun is null)
            return new BaselineComparisonResult(BaselineComparisonStatus.BaselineNotFound, null);

        var response = _comparison.Compare(run, baselineRun);
        return new BaselineComparisonResult(BaselineComparisonStatus.Ok, response);
    }
}
