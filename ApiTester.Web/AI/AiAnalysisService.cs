using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Serialization;
using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Models;

namespace ApiTester.Web.AI;

public sealed class AiAnalysisService
{
    private readonly IAiProvider _provider;
    private readonly RedactionService _redactionService;
    private readonly IAiInsightStore _insightStore;
    private readonly AiRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;

    public AiAnalysisService(
        IAiProvider provider,
        RedactionService redactionService,
        IAiInsightStore insightStore,
        AiRateLimiter rateLimiter,
        TimeProvider timeProvider)
    {
        _provider = provider;
        _redactionService = redactionService;
        _insightStore = insightStore;
        _rateLimiter = rateLimiter;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<AiInsightRecord>> AnalyzeAsync(AiAnalysisInput input, CancellationToken ct)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        var org = input.Organisation ?? throw new ArgumentNullException(nameof(input.Organisation));
        if (!org.OrgSettings.IsAiEnabled)
            throw new AiFeatureDisabledException("AI is not enabled for this organisation plan.");

        if (!_rateLimiter.TryConsume(org.OrganisationId))
            throw new AiRateLimitExceededException("AI rate limit exceeded for this organisation.");

        var prompt = BuildPrompt(input, org.RedactionRules);
        var response = await _provider.CompleteAsync(prompt, ct);
        var redactedContent = _redactionService.RedactText(response.Content, org.RedactionRules) ?? response.Content;

        var insights = AiJsonSchemas.ParseInsights(redactedContent);
        var createdUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var records = await _insightStore.CreateAsync(
            org.OrganisationId,
            input.ProjectId,
            input.Run.RunId,
            input.OperationId,
            insights.Select(i => new AiInsightCreate(i.Type, i.JsonPayload)).ToList(),
            response.ModelId,
            createdUtc,
            ct);

        return records;
    }

    private AiRequest BuildPrompt(AiAnalysisInput input, IReadOnlyList<string>? redactionRules)
    {
        var redactedRun = CreateRedactedRun(input.Run, redactionRules);
        var runContext = AiContextFactory.BuildRunExplanationContext(redactedRun);

        var operationContext = BuildOperationContext(
            input.OperationId,
            input.HttpMethod,
            input.Path,
            input.Operation);

        var runJson = JsonSerializer.Serialize(runContext, JsonDefaults.Default);
        var operationJson = JsonSerializer.Serialize(operationContext, JsonDefaults.Default);
        var policyJson = JsonSerializer.Serialize(input.Policy, JsonDefaults.Default);

        runJson = _redactionService.RedactText(runJson, redactionRules) ?? runJson;
        operationJson = _redactionService.RedactText(operationJson, redactionRules) ?? operationJson;
        policyJson = _redactionService.RedactText(policyJson, redactionRules) ?? policyJson;

        var systemPrompt = """
You are an API testing analyst. Use only the provided context.
Respond with JSON that matches the required schema. Do not include markdown.
""";

        var userPrompt = new StringBuilder()
            .AppendLine("Schema:")
            .AppendLine(AiJsonSchemas.SchemaJson)
            .AppendLine()
            .AppendLine("OpenAPI operation:")
            .AppendLine(operationJson)
            .AppendLine()
            .AppendLine("Execution policy:")
            .AppendLine(policyJson)
            .AppendLine()
            .AppendLine("Run results:")
            .AppendLine(runJson)
            .ToString();

        return new AiRequest(systemPrompt, userPrompt);
    }

    private TestRunRecord CreateRedactedRun(TestRunRecord run, IReadOnlyList<string>? redactionRules)
    {
        var redactedResult = _redactionService.RedactResult(run.Result, redactionRules);

        return new TestRunRecord
        {
            RunId = run.RunId,
            OrganisationId = run.OrganisationId,
            Actor = run.Actor,
            Environment = run.Environment,
            PolicySnapshot = run.PolicySnapshot,
            OwnerKey = run.OwnerKey,
            OperationId = run.OperationId,
            SpecId = run.SpecId,
            BaselineRunId = run.BaselineRunId,
            StartedUtc = run.StartedUtc,
            CompletedUtc = run.CompletedUtc,
            ProjectKey = run.ProjectKey,
            Result = redactedResult
        };
    }

    private static OpenApiOperationContext BuildOperationContext(
        string operationId,
        string httpMethod,
        string path,
        OpenApiOperation operation)
    {
        var parameters = operation.Parameters?
            .Select(p => new OpenApiParameterContext(
                p.Name,
                p.In?.ToString() ?? string.Empty,
                p.Required,
                p.Schema?.Type,
                p.Description))
            .ToList() ?? new List<OpenApiParameterContext>();

        var responses = operation.Responses?
            .Select(r => new OpenApiResponseContext(
                r.Key,
                r.Value.Description))
            .ToList() ?? new List<OpenApiResponseContext>();

        return new OpenApiOperationContext(
            operationId,
            httpMethod,
            path,
            operation.Summary,
            operation.Description,
            operation.Deprecated,
            operation.RequestBody is not null,
            parameters,
            responses);
    }

    private sealed record OpenApiOperationContext(
        string OperationId,
        string HttpMethod,
        string Path,
        string? Summary,
        string? Description,
        bool? Deprecated,
        bool HasRequestBody,
        IReadOnlyList<OpenApiParameterContext> Parameters,
        IReadOnlyList<OpenApiResponseContext> Responses);

    private sealed record OpenApiParameterContext(
        string Name,
        string In,
        bool Required,
        string? SchemaType,
        string? Description);

    private sealed record OpenApiResponseContext(
        string StatusCode,
        string? Description);
}
