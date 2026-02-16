using System.Text.Json;
using ApiTester.McpServer.Serialization;

namespace ApiTester.Web.AI;

public sealed class StubAiProvider : IAiProvider
{
    public Task<AiResult> ExplainApiAsync(string spec, string operationId, CancellationToken ct)
        => Task.FromResult(new AiResult(JsonSerializer.Serialize(new
        {
            summary = "Stub summary",
            inputs = "Stub inputs",
            outputs = "Stub outputs",
            auth = "Stub auth",
            gotchas = Array.Empty<string>(),
            examples = Array.Empty<object>(),
            markdown = "Stub markdown"
        }, JsonDefaults.Default), "stub"));

    public Task<AiResult> SuggestEdgeCasesAsync(string spec, string operationId, CancellationToken ct)
        => Task.FromResult(new AiResult(JsonSerializer.Serialize(new
        {
            cases = new[]
            {
                new
                {
                    name = "Stub case",
                    rationale = "Stub rationale",
                    @params = new
                    {
                        path = new { },
                        query = new { },
                        headers = new { }
                    },
                    expectedStatusRanges = new[] { "200-299" }
                }
            }
        }, JsonDefaults.Default), "stub"));

    public Task<AiResult> SummariseRunAsync(string runId, string runContext, CancellationToken ct)
        => Task.FromResult(new AiResult(JsonSerializer.Serialize(new
        {
            overallSummary = "Stub summary with no critical risk.",
            topFailures = Array.Empty<object>(),
            flakeAssessment = "No clear flake signals in stub data.",
            regressionLikelihood = new
            {
                level = "low",
                rationale = "Stub response does not indicate persistent regression."
            },
            recommendedNextActions = new[] { "Re-run to confirm." }
        }, JsonDefaults.Default), "stub"));

    public Task<AiResult> SuggestFixesAsync(string runId, string runContext, CancellationToken ct)
        => Task.FromResult(new AiResult(JsonSerializer.Serialize(new
        {
            insights = new[]
            {
                new
                {
                    type = "recommendation",
                    payload = new
                    {
                        title = "Stub recommendation",
                        detail = "Validate request body schema before calling downstream service."
                    }
                }
            }
        }, JsonDefaults.Default), "stub"));

}
