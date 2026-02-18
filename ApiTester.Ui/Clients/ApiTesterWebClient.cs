using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
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

    public async Task<RunDetailDto?> GetRun(Guid runId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/runs/{runId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var run = await response.Content.ReadFromJsonAsync<RunDetailDto>(cancellationToken: ct);
        if (run is null)
        {
            throw new InvalidOperationException("Empty response when loading run.");
        }

        return run;
    }

    public async Task<HttpResponseMessage> DownloadEvidencePackAsync(Guid runId, CancellationToken ct = default)
        => await _httpClient.GetAsync($"/api/v1/runs/{runId}/evidence-pack", ct);

    public async Task<OpenApiSpecMetadataDto?> GetOpenApiSpec(Guid projectId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/projects/{projectId}/openapi", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<OpenApiSpecMetadataDto>(cancellationToken: ct);
        if (payload is null)
        {
            throw new InvalidOperationException("Empty response when loading OpenAPI spec.");
        }

        return payload;
    }

    public async Task<IReadOnlyList<OpenApiSpecMetadataDto>> ListOpenApiSpecs(Guid projectId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<IReadOnlyList<OpenApiSpecMetadataDto>>($"/api/projects/{projectId}/specs", ct);
        if (response is null)
        {
            throw new InvalidOperationException("Empty response when loading OpenAPI specs.");
        }

        return response;
    }

    public async Task<OpenApiSpecDetailDto?> GetOpenApiSpecDetail(Guid specId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/specs/{specId}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<OpenApiSpecDetailDto>(cancellationToken: ct);
        if (payload is null)
        {
            throw new InvalidOperationException("Empty response when loading OpenAPI spec detail.");
        }

        return payload;
    }

    public async Task<OpenApiOperationListResponse> ListOperations(Guid projectId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<OpenApiOperationListResponse>($"/api/projects/{projectId}/operations", ct);
        if (response is null)
        {
            throw new InvalidOperationException("Empty response when loading operations.");
        }

        return response;
    }

    public async Task<OpenApiOperationDescribeResponse?> DescribeOperation(Guid projectId, string operationId, CancellationToken ct = default)
    {
        var requestUri = QueryHelpers.AddQueryString($"/api/projects/{projectId}/operations/describe", "operationId", operationId);
        var response = await _httpClient.GetAsync(requestUri, ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<OpenApiOperationDescribeResponse>(cancellationToken: ct);
        if (payload is null)
        {
            throw new InvalidOperationException("Empty response when describing operation.");
        }

        return payload;
    }

    public async Task<TestPlanResponse?> GetTestPlan(Guid projectId, string operationId, CancellationToken ct = default)
    {
        var response = await _httpClient.GetAsync($"/api/projects/{projectId}/testplans/{Uri.EscapeDataString(operationId)}", ct);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<TestPlanResponse>(cancellationToken: ct);
        if (payload is null)
        {
            throw new InvalidOperationException("Empty response when loading test plan.");
        }

        return payload;
    }

    public async Task<TestPlanResponse> GenerateTestPlan(Guid projectId, string operationId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"/api/projects/{projectId}/testplans/{Uri.EscapeDataString(operationId)}/generate", null, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<TestPlanResponse>(cancellationToken: ct);
        if (payload is null)
        {
            throw new InvalidOperationException("Empty response when generating test plan.");
        }

        return payload;
    }

    public async Task<RunDetailDto> ExecuteTestPlan(Guid projectId, string operationId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"/api/projects/{projectId}/runs/execute/{Uri.EscapeDataString(operationId)}", null, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<RunDetailDto>(cancellationToken: ct);
        if (payload is null)
        {
            throw new InvalidOperationException("Empty response when executing test plan.");
        }

        return payload;
    }

    public async Task<OpenApiSpecMetadataDto> ImportOpenApiSpec(Guid projectId, Stream? fileStream, string? fileName, string? path, CancellationToken ct = default)
    {
        HttpResponseMessage response;

        if (fileStream is not null)
        {
            var content = new MultipartFormDataContent();
            var streamContent = new StreamContent(fileStream);
            content.Add(streamContent, "file", string.IsNullOrWhiteSpace(fileName) ? "openapi.json" : fileName);

            if (!string.IsNullOrWhiteSpace(path))
            {
                content.Add(new StringContent(path), "path");
            }

            response = await _httpClient.PostAsync($"/api/projects/{projectId}/openapi/import", content, ct);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException("Either a file or path is required to import an OpenAPI spec.");
            }

            response = await _httpClient.PostAsJsonAsync($"/api/projects/{projectId}/openapi/import", new { path }, ct);
        }

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<OpenApiSpecMetadataDto>(cancellationToken: ct);
        if (payload is null)
        {
            throw new InvalidOperationException("Empty response when importing OpenAPI spec.");
        }

        return payload;
    }

    public async Task<ApiPolicyResponse> GetPolicy(CancellationToken ct = default)
    {
        var response = await _httpClient.GetFromJsonAsync<ApiPolicyResponse>("/api/runtime/policy", ct);
        if (response is null)
        {
            throw new InvalidOperationException("Empty response when loading policy.");
        }

        return response;
    }

    public async Task<ApiPolicyResponse> UpdatePolicy(ApiPolicyUpdateRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync("/api/runtime/policy", request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<ApiPolicyResponse>(cancellationToken: ct);
        if (payload is null)
        {
            throw new InvalidOperationException("Empty response when updating policy.");
        }

        return payload;
    }

    public async Task<AiActionResult<AiExplainResponse>> ExplainOperationAsync(Guid projectId, string operationId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/ai/explain", new AiExplainRequest(projectId.ToString(), operationId), ct);
        return await ReadAiActionResultAsync<AiExplainResponse>(response, ct);
    }

    public async Task<AiActionResult<AiRunSummaryResponse>> SummariseRunAsync(Guid runId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/api/ai/summarise-run", new AiSummariseRunRequest(runId.ToString()), ct);
        return await ReadAiActionResultAsync<AiRunSummaryResponse>(response, ct);
    }

    private static async Task<AiActionResult<T>> ReadAiActionResultAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
        {
            var payload = await response.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
            if (payload is null)
            {
                throw new InvalidOperationException("Empty AI response payload.");
            }

            return new AiActionResult<T>(payload, null);
        }

        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellationToken: ct);
        if (problem is null)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new HttpRequestException($"AI request failed with {(int)response.StatusCode}: {body}", null, response.StatusCode);
        }

        var gate = new AiGateFailure(
            (int)response.StatusCode,
            problem.Title ?? "AI request blocked",
            problem.Detail ?? "The request was blocked by server-side plan or quota checks.");

        return new AiActionResult<T>(default, gate);
    }
}

public sealed record AiActionResult<T>(T? Payload, AiGateFailure? Gate)
{
    public bool IsSuccess => Payload is not null;
}

public sealed record AiGateFailure(int StatusCode, string Title, string Detail);

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
    int ExpectedBlocked,
    int Flaky,
    int RealFail,
    long TotalDurationMs,
    ResultClassificationSummary ClassificationSummary);

public sealed record RunDetailDto(
    Guid RunId,
    string ProjectKey,
    string OperationId,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    TestRunResult Result);

public sealed record TestRunResult(
    string OperationId,
    int TotalCases,
    int Passed,
    int Failed,
    int Blocked,
    long TotalDurationMs,
    ResultClassificationSummary ClassificationSummary,
    IReadOnlyList<TestCaseResult> Results);

public sealed record TestCaseResult(
    string Name,
    bool Blocked,
    string? BlockReason,
    string? Url,
    string? Method,
    int? StatusCode,
    long DurationMs,
    bool Pass,
    string? FailureReason,
    object? FailureDetails,
    string? ValidationUnavailableReason,
    string? ResponseSnippet,
    bool IsFlaky,
    string? FlakeReasonCategory,
    ResultClassification Classification);

public sealed record ResultClassificationSummary(
    int Pass,
    int Fail,
    int BlockedExpected,
    int BlockedUnexpected,
    int FlakyExternal);

public enum ResultClassification
{
    Pass,
    Fail,
    BlockedExpected,
    BlockedUnexpected,
    FlakyExternal
}

public sealed record OpenApiSpecMetadataDto(
    Guid SpecId,
    Guid ProjectId,
    string Title,
    string Version,
    string SpecHash,
    DateTime UploadedUtc);

public sealed record OpenApiSpecDetailDto(
    Guid SpecId,
    Guid ProjectId,
    string Title,
    string Version,
    string SpecJson,
    string SpecHash,
    DateTime UploadedUtc);

public sealed record OpenApiOperationListResponse(IReadOnlyList<OpenApiOperationSummaryDto> Operations);

public sealed record OpenApiOperationSummaryDto(
    string OperationId,
    string Method,
    string Path,
    string Summary,
    string Description,
    bool RequiresAuth);

public sealed record OpenApiOperationDescribeResponse(
    string OperationId,
    string Method,
    string Path,
    string Summary,
    string Description,
    bool RequiresAuth,
    IReadOnlyList<OpenApiOperationParameterDto> Parameters,
    OpenApiOperationRequestBodyDto? RequestBody,
    IReadOnlyDictionary<string, OpenApiOperationResponseDto> Responses);

public sealed record OpenApiOperationParameterDto(
    string Name,
    string In,
    bool Required,
    string Description,
    OpenApiSchemaDto? Schema);

public sealed record OpenApiOperationRequestBodyDto(
    bool Required,
    string Description,
    IReadOnlyDictionary<string, OpenApiOperationContentDto> Content);

public sealed record OpenApiOperationResponseDto(
    string Description,
    IReadOnlyDictionary<string, OpenApiOperationContentDto> Content);

public sealed record OpenApiOperationContentDto(OpenApiSchemaDto? Schema);

public sealed record OpenApiSchemaDto(
    string Type,
    string Format,
    bool Nullable,
    OpenApiSchemaItemDto? Items);

public sealed record OpenApiSchemaItemDto(string Type, string Format);

public sealed record TestPlanResponse(
    Guid ProjectId,
    string OperationId,
    string PlanJson,
    DateTime CreatedUtc);

public sealed record ApiPolicyResponse(
    bool DryRun,
    IReadOnlyList<string> AllowedBaseUrls,
    IReadOnlyList<string> AllowedMethods,
    int TimeoutSeconds,
    int MaxRequestBodyBytes,
    int MaxResponseBodyBytes,
    bool ValidateSchema,
    bool BlockLocalhost,
    bool BlockPrivateNetworks,
    bool RetryOnFlake,
    int MaxRetries);

public sealed record ApiPolicyUpdateRequest(
    bool? DryRun,
    IReadOnlyList<string>? AllowedBaseUrls,
    IReadOnlyList<string>? AllowedMethods,
    int? TimeoutSeconds,
    int? MaxRequestBodyBytes,
    int? MaxResponseBodyBytes,
    bool? ValidateSchema,
    bool? BlockLocalhost,
    bool? BlockPrivateNetworks,
    bool? RetryOnFlake,
    int? MaxRetries);

public sealed record AiExplainRequest(string ProjectId, string OperationId);

public sealed record AiSummariseRunRequest(string RunId);

public sealed record AiExplainExampleDto(string Title, string Content);

public sealed record AiExplainResponse(
    Guid ProjectId,
    string OperationId,
    string Summary,
    string Inputs,
    string Outputs,
    string Auth,
    IReadOnlyList<string> Gotchas,
    IReadOnlyList<AiExplainExampleDto> Examples,
    string Markdown);

public sealed record AiRunSummaryEvidenceRefDto(string CaseName, string? FailureReason);

public sealed record AiRunSummaryFailureDto(string Title, IReadOnlyList<AiRunSummaryEvidenceRefDto> EvidenceRefs);

public sealed record AiRunSummaryRegressionLikelihoodDto(string Level, string Rationale);

public sealed record AiRunSummaryResponse(
    Guid RunId,
    string OverallSummary,
    IReadOnlyList<AiRunSummaryFailureDto> TopFailures,
    string FlakeAssessment,
    AiRunSummaryRegressionLikelihoodDto RegressionLikelihood,
    IReadOnlyList<string> RecommendedNextActions);
