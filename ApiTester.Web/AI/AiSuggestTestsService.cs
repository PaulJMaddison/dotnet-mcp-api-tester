using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Serialization;
using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Models;

namespace ApiTester.Web.AI;

public sealed class AiSuggestTestsService
{
    private readonly IAiProvider _provider;
    private readonly RedactionService _redactionService;
    private readonly ITestPlanDraftStore _draftStore;
    private readonly AiRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;

    public AiSuggestTestsService(
        IAiProvider provider,
        RedactionService redactionService,
        ITestPlanDraftStore draftStore,
        AiRateLimiter rateLimiter,
        TimeProvider timeProvider)
    {
        _provider = provider;
        _redactionService = redactionService;
        _draftStore = draftStore;
        _rateLimiter = rateLimiter;
        _timeProvider = timeProvider;
    }

    public async Task<AiSuggestTestsResult> SuggestAsync(AiSuggestTestsInput input, CancellationToken ct)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        var org = input.Organisation ?? throw new ArgumentNullException(nameof(input.Organisation));
        if (!org.OrgSettings.IsAiEnabled)
            throw new AiFeatureDisabledException("AI is not enabled for this organisation plan.");

        if (!_rateLimiter.TryConsume(org.OrganisationId))
            throw new AiRateLimitExceededException("AI rate limit exceeded for this organisation.");

        var prompt = BuildPrompt(input, org.RedactionRules);
        var response = await _provider.SuggestEdgeCasesAsync(prompt, input.OperationId, ct);
        var redactedContent = _redactionService.RedactText(response.Content, org.RedactionRules) ?? response.Content;

        var suggestions = AiSuggestTestsSchemas.ParseSuggestions(redactedContent);
        var plan = BuildPlan(input, suggestions);
        var redactedPlan = _redactionService.RedactPlan(plan, org.RedactionRules);
        var planJson = JsonSerializer.Serialize(redactedPlan, JsonDefaults.Default);
        var createdUtc = _timeProvider.GetUtcNow().UtcDateTime;
        var record = await _draftStore.CreateAsync(input.ProjectId, input.OperationId, planJson, createdUtc, ct);

        return new AiSuggestTestsResult(record, redactedPlan, response.ModelId, createdUtc);
    }

    private string BuildPrompt(AiSuggestTestsInput input, IReadOnlyList<string>? redactionRules)
    {
        var context = BuildOperationContext(
            input.OperationId,
            input.HttpMethod,
            input.Path,
            input.Operation);

        var contextJson = JsonSerializer.Serialize(context, JsonDefaults.Default);
        contextJson = _redactionService.RedactText(contextJson, redactionRules) ?? contextJson;

        var systemPrompt = """
You are an API testing assistant. Use only the provided context.
Respond with JSON that matches the required schema. Do not include markdown.
""";

        var userPrompt = new StringBuilder()
            .AppendLine("Schema:")
            .AppendLine(AiSuggestTestsSchemas.SchemaJson)
            .AppendLine()
            .AppendLine("OpenAPI operation:")
            .AppendLine(contextJson)
            .ToString();

        return $"{systemPrompt}\n\n{userPrompt}";
    }

    private static AiSuggestTestsOperationContext BuildOperationContext(
        string operationId,
        string httpMethod,
        string path,
        OpenApiOperation operation)
    {
        var parameters = operation.Parameters?
            .Select(p => new AiSuggestTestsParameter(
                p.Name,
                p.In?.ToString() ?? string.Empty,
                p.Required,
                p.Schema?.Type,
                p.Description))
            .ToList() ?? new List<AiSuggestTestsParameter>();

        var responses = operation.Responses?
            .Select(r => new AiSuggestTestsResponse(
                r.Key,
                r.Value.Description))
            .ToList() ?? new List<AiSuggestTestsResponse>();

        return new AiSuggestTestsOperationContext(
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

    private static TestPlan BuildPlan(AiSuggestTestsInput input, IReadOnlyList<AiSuggestedTestCase> suggestions)
    {
        var plan = new TestPlan
        {
            OperationId = input.OperationId,
            Method = input.HttpMethod,
            PathTemplate = input.Path,
            Cases = new List<TestCase>()
        };

        var index = 1;
        foreach (var suggestion in suggestions)
        {
            var name = string.IsNullOrWhiteSpace(suggestion.Name)
                ? $"AI suggestion {index}"
                : suggestion.Name.Trim();

            if (!string.IsNullOrWhiteSpace(suggestion.Rationale))
                name = $"{name} — {suggestion.Rationale.Trim()}";

            var expectedStatusCodes = ExpandStatusRanges(suggestion.ExpectedStatusRanges);
            var parameters = suggestion.Params;

            plan.Cases.Add(new TestCase
            {
                Name = name,
                PathParams = NormalizeMap(parameters.Path),
                QueryParams = NormalizeMap(parameters.Query),
                Headers = NormalizeMap(parameters.Headers),
                BodyJson = string.IsNullOrWhiteSpace(parameters.BodyJson) ? null : parameters.BodyJson,
                ExpectedStatusCodes = expectedStatusCodes
            });

            index++;
        }

        return plan;
    }

    private static List<int> ExpandStatusRanges(IReadOnlyList<string> ranges)
    {
        var statuses = new SortedSet<int>();

        foreach (var range in ranges)
        {
            if (string.IsNullOrWhiteSpace(range))
                throw new AiSchemaValidationException("Expected status ranges cannot be empty.");

            var trimmed = range.Trim().ToLowerInvariant();
            if (trimmed.Length == 3 && trimmed.EndsWith("xx", StringComparison.Ordinal) && char.IsDigit(trimmed[0]))
            {
                var baseValue = (trimmed[0] - '0') * 100;
                AddRange(statuses, baseValue, baseValue + 99);
                continue;
            }

            if (trimmed.Contains('-', StringComparison.Ordinal))
            {
                var parts = trimmed.Split('-', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !int.TryParse(parts[0], out var start) || !int.TryParse(parts[1], out var end))
                    throw new AiSchemaValidationException($"Invalid status range '{range}'.");

                AddRange(statuses, start, end);
                continue;
            }

            if (!int.TryParse(trimmed, out var code))
                throw new AiSchemaValidationException($"Invalid status range '{range}'.");

            AddRange(statuses, code, code);
        }

        if (statuses.Count == 0)
            throw new AiSchemaValidationException("Expected status ranges must include at least one status code.");

        return statuses.ToList();
    }

    private static void AddRange(SortedSet<int> statuses, int start, int end)
    {
        if (start < 100 || end > 599 || end < start)
            throw new AiSchemaValidationException($"Invalid status range '{start}-{end}'.");

        for (var code = start; code <= end; code++)
            statuses.Add(code);
    }

    private static Dictionary<string, string> NormalizeMap(Dictionary<string, string> source)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in source)
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            result[key.Trim()] = value?.Trim() ?? string.Empty;
        }

        return result;
    }
}

public sealed record AiSuggestTestsInput(
    OrganisationRecord Organisation,
    Guid ProjectId,
    string OperationId,
    string HttpMethod,
    string Path,
    OpenApiOperation Operation);

public sealed record AiSuggestTestsResult(
    TestPlanDraftRecord Draft,
    TestPlan Plan,
    string? ModelId,
    DateTime CreatedUtc);

public sealed record AiSuggestTestsOperationContext(
    string OperationId,
    string HttpMethod,
    string Path,
    string? Summary,
    string? Description,
    bool? Deprecated,
    bool HasRequestBody,
    IReadOnlyList<AiSuggestTestsParameter> Parameters,
    IReadOnlyList<AiSuggestTestsResponse> Responses);

public sealed record AiSuggestTestsParameter(
    string Name,
    string In,
    bool Required,
    string? SchemaType,
    string? Description);

public sealed record AiSuggestTestsResponse(string StatusCode, string? Description);
