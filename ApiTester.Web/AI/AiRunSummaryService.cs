using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Persistence.Stores;
using ApiTester.McpServer.Serialization;
using ApiTester.McpServer.Services;

namespace ApiTester.Web.AI;

public sealed class AiRunSummaryService
{
    private readonly IAiProvider _provider;
    private readonly RedactionService _redactionService;
    private readonly IAiInsightStore _insightStore;
    private readonly AiRateLimiter _rateLimiter;
    private readonly TimeProvider _timeProvider;

    public AiRunSummaryService(
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

    public async Task<AiRunSummaryResult> SummariseAsync(AiRunSummaryInput input, CancellationToken ct)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));

        var org = input.Organisation ?? throw new ArgumentNullException(nameof(input.Organisation));
        if (!org.OrgSettings.IsAiEnabled)
            throw new AiFeatureDisabledException("AI is not enabled for this organisation plan.");

        if (!_rateLimiter.TryConsume(org.OrganisationId))
            throw new AiRateLimitExceededException("AI rate limit exceeded for this organisation.");

        var prompt = BuildPrompt(input, org.RedactionRules);
        var response = await _provider.SummariseRunAsync(input.Run.RunId.ToString(), prompt, ct);
        var redactedContent = _redactionService.RedactText(response.Content, org.RedactionRules) ?? response.Content;

        var payload = AiRunSummarySchemas.ParseSummary(redactedContent);
        ValidateEvidenceRefs(payload, input.Run);

        var createdUtc = _timeProvider.GetUtcNow().UtcDateTime;
        await _insightStore.CreateAsync(
            org.OrganisationId,
            input.ProjectId,
            input.Run.RunId,
            input.Run.OperationId,
            new[] { new AiInsightCreate("run-summary", redactedContent) },
            response.ModelId,
            createdUtc,
            ct);

        return new AiRunSummaryResult(payload, redactedContent, response.ModelId, createdUtc);
    }

    private string BuildPrompt(AiRunSummaryInput input, IReadOnlyList<string>? redactionRules)
    {
        var redactedRun = CreateRedactedRun(input.Run, redactionRules);
        var runContext = AiContextFactory.BuildRunExplanationContext(redactedRun);
        var runJson = JsonSerializer.Serialize(runContext, JsonDefaults.Default);
        runJson = _redactionService.RedactText(runJson, redactionRules) ?? runJson;

        var systemPrompt = """
You are an API test run analyst. Use only the provided run data.
Respond with JSON that matches the required schema. Do not include markdown.
Evidence refs must cite caseName values from the run results. Separate flake versus regression analysis.
""";

        var userPrompt = new StringBuilder()
            .AppendLine("Schema:")
            .AppendLine(AiRunSummarySchemas.SchemaJson)
            .AppendLine()
            .AppendLine("Run results:")
            .AppendLine(runJson)
            .AppendLine()
            .AppendLine("Instructions:")
            .AppendLine("- Provide overall summary with root cause hints and ship-risk summary.")
            .AppendLine("- Top failures must include evidenceRefs that cite failing caseName values.")
            .AppendLine("- Include flake assessment and regression likelihood.")
            .AppendLine("- Recommend next actions as a short list.")
            .ToString();

        return $"{systemPrompt}\n\n{userPrompt}";
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

    private static void ValidateEvidenceRefs(AiRunSummaryPayload payload, TestRunRecord run)
    {
        var caseNames = new HashSet<string>(
            run.Result.Results.Select(r => r.Name),
            StringComparer.OrdinalIgnoreCase);

        foreach (var failure in payload.TopFailures)
        {
            foreach (var evidence in failure.EvidenceRefs)
            {
                if (!caseNames.Contains(evidence.CaseName))
                    throw new AiSchemaValidationException($"Evidence ref caseName '{evidence.CaseName}' not found in run results.");
            }
        }
    }
}

public sealed record AiRunSummaryInput(
    OrganisationRecord Organisation,
    Guid ProjectId,
    TestRunRecord Run);

public sealed record AiRunSummaryResult(
    AiRunSummaryPayload Payload,
    string RawResponse,
    string ModelId,
    DateTime CreatedUtc);
