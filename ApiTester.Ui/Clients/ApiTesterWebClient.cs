using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;

namespace ApiTester.Ui.Clients;

public sealed class ApiTesterWebClient
{
    private readonly HttpClient _httpClient;

    public ApiTesterWebClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ProjectListResponse> ListProjects(int take, CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<ProjectListResponse>($"/api/projects?take={take}", ct);
        if (response is null)
        {
            throw new InvalidOperationException("Empty response when loading projects.");
        }

        return response;
    }

    public async Task<ProjectDto> GetProject(Guid projectId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<ProjectDto>($"/api/projects/{projectId}", ct);
        if (response is null)
        {
            throw new InvalidOperationException("Empty response when loading project.");
        }

        return response;
    }

    public async Task<RunSummaryResponse> ListRuns(string projectKey, int take, string? operationId, CancellationToken ct = default)
    {
        var queryParams = new Dictionary<string, string?>
        {
            ["projectKey"] = projectKey,
            ["take"] = take.ToString()
        };

        if (!string.IsNullOrWhiteSpace(operationId))
        {
            queryParams["operationId"] = operationId;
        }

        var requestUri = QueryHelpers.AddQueryString("/api/runs", queryParams);
        var response = await _httpClient.GetFromJsonAsync<RunSummaryResponse>(requestUri, ct);
        if (response is null)
        {
            throw new InvalidOperationException("Empty response when loading runs.");
        }

        return response;
    }
}

public sealed record ProjectDto(Guid ProjectId, string Name, string ProjectKey, DateTime CreatedUtc);

public sealed record PageMetadata(int Total, int PageSize, string? NextPageToken);

public sealed record ProjectListResponse(IReadOnlyList<ProjectDto> Projects, PageMetadata Metadata);

public sealed record RunSummaryResponse(string ProjectKey, IReadOnlyList<RunSummaryDto> Runs, PageMetadata Metadata);

public sealed record RunSummaryDto(
    Guid RunId,
    string ProjectKey,
    string OperationId,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    RunSummary Snapshot);

public sealed record RunSummary(
    int TotalCases,
    int Passed,
    int Failed,
    int Blocked,
    long TotalDurationMs);
