using ApiTester.Site.Components.Pages.App;
using ApiTester.Site.Models;
using ApiTester.Site.Services;
using Bunit;

namespace ApiTester.Site.Tests;

public class RunDetailRenderingTests
{
    [Fact]
    public void RunDetail_RendersSummaryAndCases()
    {
        using var context = new TestContext();
        var runId = Guid.NewGuid();
        var client = new FakeApiTesterWebClient
        {
            RunDetailResponse = ApiResult<RunDetailDto>.Success(new RunDetailDto(
                runId,
                "demo-project",
                "GET /widgets",
                DateTimeOffset.UtcNow.AddMinutes(-5),
                DateTimeOffset.UtcNow,
                new TestRunResult
                {
                    OperationId = "GET /widgets",
                    TotalCases = 2,
                    Passed = 1,
                    Failed = 1,
                    Blocked = 0,
                    TotalDurationMs = 1200,
                    Results = new List<TestCaseResult>
                    {
                        new()
                        {
                            Name = "Happy path",
                            Method = "GET",
                            Url = "/widgets",
                            Pass = true,
                            StatusCode = 200,
                            DurationMs = 500
                        },
                        new()
                        {
                            Name = "Not found",
                            Method = "GET",
                            Url = "/widgets/404",
                            Pass = false,
                            StatusCode = 404,
                            DurationMs = 300,
                            FailureReason = "Expected 200"
                        }
                    }
                }))
        };

        context.Services.AddSingleton<IApiTesterWebClient>(client);

        var cut = context.RenderComponent<RunDetail>(parameters => parameters.Add(p => p.RunId, runId.ToString()));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Case results", cut.Markup);
            Assert.Contains("Happy path", cut.Markup);
            Assert.Contains("Not found", cut.Markup);
            Assert.Contains("GET /widgets", cut.Markup);
        });
    }

    [Fact]
    public void RunDetail_ShowsErrorMessageWhenClientFails()
    {
        using var context = new TestContext();
        var runId = Guid.NewGuid();
        var client = new FakeApiTesterWebClient
        {
            RunDetailResponse = ApiResult<RunDetailDto>.Failure("Run is unavailable.", "Upstream error")
        };

        context.Services.AddSingleton<IApiTesterWebClient>(client);

        var cut = context.RenderComponent<RunDetail>(parameters => parameters.Add(p => p.RunId, runId.ToString()));

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Unable to load run details", cut.Markup);
            Assert.Contains("Run is unavailable.", cut.Markup);
        });
    }

    private sealed class FakeApiTesterWebClient : IApiTesterWebClient
    {
        public ApiResult<ProjectListResponse> ProjectResponse { get; set; } = ApiResult<ProjectListResponse>.Failure("Not used", null);
        public ApiResult<RunSummaryResponse> RunSummaryResponse { get; set; } = ApiResult<RunSummaryResponse>.Failure("Not used", null);
        public ApiResult<RunDetailDto> RunDetailResponse { get; set; } = ApiResult<RunDetailDto>.Failure("Not set", null);

        public Task<ApiResult<ProjectListResponse>> GetProjectsAsync(CancellationToken cancellationToken)
            => Task.FromResult(ProjectResponse);

        public Task<ApiResult<RunSummaryResponse>> GetRunsAsync(string projectKey, string? operationId, int? take, CancellationToken cancellationToken)
            => Task.FromResult(RunSummaryResponse);

        public Task<ApiResult<RunDetailDto>> GetRunDetailAsync(Guid runId, CancellationToken cancellationToken)
            => Task.FromResult(RunDetailResponse);
    }
}
