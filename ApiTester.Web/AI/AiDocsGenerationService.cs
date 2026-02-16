using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;
using ApiTester.McpServer.Services;
using Microsoft.OpenApi.Models;

namespace ApiTester.Web.AI;

public sealed class AiDocsGenerationService
{
    private const int MaxRunsPerOperation = 3;
    private const int MaxCasesPerRun = 3;

    private readonly IAiProvider _provider;
    private readonly RedactionService _redactionService;
    private readonly AiRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;

    public AiDocsGenerationService(
        IAiProvider provider,
        RedactionService redactionService,
        AiRateLimiter rateLimiter,
        TimeProvider timeProvider)
    {
        _provider = provider;
        _redactionService = redactionService;
        _rateLimiter = rateLimiter;
        _timeProvider = timeProvider;
    }

    public async Task<AiDocsGenerationResult> GenerateAsync(AiDocsGenerationInput input, CancellationToken ct)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        var org = input.Organisation ?? throw new ArgumentNullException(nameof(input.Organisation));
        if (!org.OrgSettings.IsAiEnabled)
            throw new AiFeatureDisabledException("AI is not enabled for this organisation plan.");

        if (!_rateLimiter.TryConsume(org.OrganisationId))
            throw new AiRateLimitExceededException("AI rate limit exceeded for this organisation.");

        var operations = BuildOperationContexts(input.Document);
        var runExamples = BuildRunExamples(input.Runs, org.RedactionRules);

        var context = new AiDocsContext(
            input.Project.ProjectId,
            input.Project.Name,
            input.Spec.Title,
            input.Spec.Version,
            operations,
            runExamples);

        var contextJson = JsonSerializer.Serialize(context, JsonDefaults.Default);
        contextJson = _redactionService.RedactText(contextJson, org.RedactionRules) ?? contextJson;

        var prompt = BuildPrompt(contextJson);
        var response = await _provider.ExplainApiAsync(prompt, "docs-generation", ct);
        var redactedContent = _redactionService.RedactText(response.Content, org.RedactionRules) ?? response.Content;

        var payload = AiDocsSchemas.ParseDocs(redactedContent);
        ValidatePayload(payload, operations, runExamples);

        var createdUtc = _timeProvider.GetUtcNow().UtcDateTime;
        return new AiDocsGenerationResult(payload, redactedContent, response.ModelId, createdUtc);
    }

    private static string BuildPrompt(string contextJson)
    {
        var systemPrompt = """
You are an API documentation generator. Use only the provided context.
Return JSON that matches the required schema exactly.
Do not invent endpoints or example values.
Examples must reference the provided runId and caseName values.
""";

        var userPrompt = new StringBuilder()
            .AppendLine("Schema:")
            .AppendLine(AiDocsSchemas.SchemaJson)
            .AppendLine()
            .AppendLine("Context:")
            .AppendLine(contextJson)
            .AppendLine()
            .AppendLine("Instructions:")
            .AppendLine("- Generate one section per operationId in the context.")
            .AppendLine("- Include markdown for each section with headings, parameters, responses, and examples.")
            .AppendLine("- Use only the run examples provided; do not fabricate data.")
            .ToString();

        return $"{systemPrompt}\n\n{userPrompt}";
    }

    private static List<AiDocsOperationContext> BuildOperationContexts(OpenApiDocument document)
    {
        var list = new List<AiDocsOperationContext>();

        foreach (var path in document.Paths)
        {
            foreach (var operation in path.Value.Operations)
            {
                var operationId = operation.Value.OperationId;
                if (string.IsNullOrWhiteSpace(operationId))
                    continue;

                var parameters = operation.Value.Parameters?
                    .Select(p => new AiDocsParameterContext(
                        p.Name,
                        p.In?.ToString() ?? string.Empty,
                        p.Required,
                        p.Schema?.Type,
                        p.Schema?.Format,
                        p.Description))
                    .ToList() ?? [];

                var responses = operation.Value.Responses?
                    .Select(r => new AiDocsResponseContext(
                        r.Key,
                        r.Value.Description,
                        r.Value.Content?.Select(c => c.Key).ToList() ?? []))
                    .ToList() ?? [];

                var requestBody = operation.Value.RequestBody is null
                    ? null
                    : new AiDocsRequestBodyContext(
                        operation.Value.RequestBody.Description,
                        operation.Value.RequestBody.Required,
                        operation.Value.RequestBody.Content?.Select(c => c.Key).ToList() ?? []);

                list.Add(new AiDocsOperationContext(
                    operationId.Trim(),
                    operation.Key.ToString(),
                    path.Key,
                    operation.Value.Summary,
                    operation.Value.Description,
                    operation.Value.Deprecated,
                    operation.Value.Tags?.Select(tag => tag.Name).Where(tag => !string.IsNullOrWhiteSpace(tag)).ToList() ?? [],
                    parameters,
                    requestBody,
                    responses));
            }
        }

        return list;
    }

    private IReadOnlyList<AiDocsOperationExamplesContext> BuildRunExamples(
        IReadOnlyList<TestRunRecord> runs,
        IReadOnlyList<string>? redactionRules)
    {
        var grouped = runs
            .Where(run => !string.IsNullOrWhiteSpace(run.OperationId))
            .GroupBy(run => run.OperationId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group
                .OrderByDescending(run => run.StartedUtc)
                .Take(MaxRunsPerOperation)
                .ToList(), StringComparer.OrdinalIgnoreCase);

        var list = new List<AiDocsOperationExamplesContext>();

        foreach (var (operationId, operationRuns) in grouped)
        {
            var runExamples = new List<AiDocsRunExampleContext>();
            foreach (var run in operationRuns)
            {
                var redactedResult = _redactionService.RedactResult(run.Result, redactionRules);
                var cases = redactedResult.Results
                    .OrderBy(result => result.Name, StringComparer.OrdinalIgnoreCase)
                    .Take(MaxCasesPerRun)
                    .Select(result => new AiDocsCaseExampleContext(
                        result.Name,
                        result.Method,
                        _redactionService.RedactText(result.Url, redactionRules) ?? result.Url,
                        result.StatusCode,
                        result.Pass,
                        _redactionService.RedactText(result.FailureReason, redactionRules) ?? result.FailureReason,
                        result.ResponseSnippet))
                    .ToList();

                runExamples.Add(new AiDocsRunExampleContext(
                    run.RunId,
                    run.StartedUtc,
                    cases));
            }

            list.Add(new AiDocsOperationExamplesContext(operationId, runExamples));
        }

        return list;
    }

    private static void ValidatePayload(
        AiDocsPayload payload,
        IReadOnlyList<AiDocsOperationContext> operations,
        IReadOnlyList<AiDocsOperationExamplesContext> examples)
    {
        var expectedOps = new HashSet<string>(operations.Select(o => o.OperationId), StringComparer.OrdinalIgnoreCase);
        var seenOps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var section in payload.Sections)
        {
            if (string.IsNullOrWhiteSpace(section.OperationId))
                throw new AiSchemaValidationException("Section operationId is required.");

            var operationId = section.OperationId.Trim();
            if (!expectedOps.Contains(operationId))
                throw new AiSchemaValidationException($"Unknown operationId '{section.OperationId}' in docs payload.");

            if (!seenOps.Add(operationId))
                throw new AiSchemaValidationException($"Duplicate section for operationId '{section.OperationId}'.");

            if (string.IsNullOrWhiteSpace(section.Markdown))
                throw new AiSchemaValidationException($"Section markdown is required for operationId '{section.OperationId}'.");
        }

        if (seenOps.Count != expectedOps.Count)
            throw new AiSchemaValidationException("Docs payload must include a section for every operation in the OpenAPI spec.");

        var allowedExamples = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var runOperationLookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var operationExamples in examples)
        {
            foreach (var run in operationExamples.Runs)
            {
                var runKey = NormalizeRunId(run.RunId.ToString("D"));
                runOperationLookup[runKey] = operationExamples.OperationId;

                foreach (var example in run.Cases)
                {
                    var key = BuildExampleKey(run.RunId, example.CaseName);
                    allowedExamples.Add(key);
                }
            }
        }

        foreach (var section in payload.Sections)
        {
            foreach (var example in section.Examples)
            {
                var normalizedRunId = NormalizeRunId(example.RunId);
                if (!runOperationLookup.TryGetValue(normalizedRunId, out var operationId))
                    throw new AiSchemaValidationException($"Example run '{example.RunId}' was not found in run history.");

                if (!string.Equals(operationId, section.OperationId, StringComparison.OrdinalIgnoreCase))
                    throw new AiSchemaValidationException($"Example run '{example.RunId}' does not match operationId '{section.OperationId}'.");

                var key = BuildExampleKey(normalizedRunId, example.CaseName);
                if (!allowedExamples.Contains(key))
                    throw new AiSchemaValidationException($"Example '{example.CaseName}' for run '{example.RunId}' was not found in run history.");
            }
        }
    }

    private static string BuildExampleKey(Guid runId, string caseName)
        => $"{runId:D}:{caseName}";

    private static string BuildExampleKey(string runId, string caseName)
        => $"{NormalizeRunId(runId)}:{caseName}";

    private static string NormalizeRunId(string runId)
        => Guid.TryParse(runId, out var id) ? id.ToString("D") : runId.Trim();

    private sealed record AiDocsContext(
        Guid ProjectId,
        string ProjectName,
        string SpecTitle,
        string SpecVersion,
        IReadOnlyList<AiDocsOperationContext> Operations,
        IReadOnlyList<AiDocsOperationExamplesContext> RunExamples);

    private sealed record AiDocsOperationContext(
        string OperationId,
        string Method,
        string Path,
        string? Summary,
        string? Description,
        bool? Deprecated,
        IReadOnlyList<string> Tags,
        IReadOnlyList<AiDocsParameterContext> Parameters,
        AiDocsRequestBodyContext? RequestBody,
        IReadOnlyList<AiDocsResponseContext> Responses);

    private sealed record AiDocsParameterContext(
        string Name,
        string In,
        bool Required,
        string? SchemaType,
        string? SchemaFormat,
        string? Description);

    private sealed record AiDocsRequestBodyContext(
        string? Description,
        bool Required,
        IReadOnlyList<string> ContentTypes);

    private sealed record AiDocsResponseContext(
        string StatusCode,
        string? Description,
        IReadOnlyList<string> ContentTypes);

    private sealed record AiDocsOperationExamplesContext(
        string OperationId,
        IReadOnlyList<AiDocsRunExampleContext> Runs);

    private sealed record AiDocsRunExampleContext(
        Guid RunId,
        DateTimeOffset StartedUtc,
        IReadOnlyList<AiDocsCaseExampleContext> Cases);

    private sealed record AiDocsCaseExampleContext(
        string CaseName,
        string? Method,
        string? Url,
        int? StatusCode,
        bool Pass,
        string? FailureReason,
        string? ResponseSnippet);
}

public sealed record AiDocsGenerationInput(
    OrganisationRecord Organisation,
    ProjectRecord Project,
    OpenApiSpecRecord Spec,
    OpenApiDocument Document,
    IReadOnlyList<TestRunRecord> Runs);

public sealed record AiDocsGenerationResult(
    AiDocsPayload Payload,
    string RawResponse,
    string ModelId,
    DateTime CreatedUtc);
