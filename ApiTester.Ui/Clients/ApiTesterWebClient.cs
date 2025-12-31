using System.Net.Http.Json;

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
}

public sealed record ProjectDto(Guid ProjectId, string Name, string ProjectKey, DateTime CreatedUtc);

public sealed record PageMetadata(int Total, int PageSize, string? NextPageToken);

public sealed record ProjectListResponse(IReadOnlyList<ProjectDto> Projects, PageMetadata Metadata);
