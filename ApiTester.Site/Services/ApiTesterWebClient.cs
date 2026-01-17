using System.Net.Http.Json;
using ApiTester.Site.Models;

namespace ApiTester.Site.Services;

public interface IApiTesterWebClient
{
    Task<ApiResult<ProjectListResponse>> GetProjectsAsync(CancellationToken cancellationToken);
    Task<ApiResult<RunSummaryResponse>> GetRunsAsync(string projectKey, string? operationId, int? take, CancellationToken cancellationToken);
    Task<ApiResult<RunDetailDto>> GetRunDetailAsync(Guid runId, CancellationToken cancellationToken);
}

public sealed class ApiTesterWebClient : IApiTesterWebClient
{
    private readonly HttpClient _httpClient;

    public ApiTesterWebClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public Task<ApiResult<ProjectListResponse>> GetProjectsAsync(CancellationToken cancellationToken)
        => GetAsync<ProjectListResponse>("/api/projects", cancellationToken);

    public Task<ApiResult<RunSummaryResponse>> GetRunsAsync(string projectKey, string? operationId, int? take, CancellationToken cancellationToken)
    {
        var query = new List<string> { $"projectKey={Uri.EscapeDataString(projectKey)}" };

        if (!string.IsNullOrWhiteSpace(operationId))
        {
            query.Add($"operationId={Uri.EscapeDataString(operationId)}");
        }

        if (take is > 0)
        {
            query.Add($"take={take.Value}");
        }

        var path = query.Count > 0 ? $"/api/runs?{string.Join("&", query)}" : "/api/runs";
        return GetAsync<RunSummaryResponse>(path, cancellationToken);
    }

    public Task<ApiResult<RunDetailDto>> GetRunDetailAsync(Guid runId, CancellationToken cancellationToken)
        => GetAsync<RunDetailDto>($"/api/runs/{runId}", cancellationToken);

    private async Task<ApiResult<T>> GetAsync<T>(string requestUri, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync(cancellationToken);
                return ApiResult<T>.Failure($"Request failed with {(int)response.StatusCode} {response.ReasonPhrase}.", details);
            }

            var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken: cancellationToken);
            return payload is null
                ? ApiResult<T>.Failure("ApiTester Web returned an empty response.", null)
                : ApiResult<T>.Success(payload);
        }
        catch (Exception ex)
        {
            return ApiResult<T>.Failure("Request failed to reach ApiTester Web.", ex.ToString());
        }
    }
}

public sealed record ApiResult<T>(T? Data, ApiError? Error)
{
    public bool IsSuccess => Error is null && Data is not null;

    public static ApiResult<T> Success(T data) => new(data, null);

    public static ApiResult<T> Failure(string message, string? details)
        => new(default, new ApiError(message, details));
}

public sealed record ApiError(string Message, string? Details);
