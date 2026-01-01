namespace ApiTester.AI;

public static class AiPromptTemplates
{
    private const string DeterministicGuidance = """
You are a deterministic assistant. Use only the data provided in the JSON payload.
If a value is missing, respond with \"Unknown\".
Do not invent details or speculate.
""";

    public static AiPrompt BuildRunExplanationPrompt(string runJson)
    {
        var system = $"""
{DeterministicGuidance}
You explain API test runs for engineering teams.
Return a concise markdown summary.
""";

        var user = $"""
Explain the following API test run.
Include: overall outcome, timing, pass/fail/blocked counts, top failures, and environment/base URL if present.

JSON:
{runJson}
""";

        return new AiPrompt(system, user);
    }

    public static AiPrompt BuildSpecSummaryPrompt(string specJson)
    {
        var system = $"""
{DeterministicGuidance}
You summarize OpenAPI specifications for engineers and product managers.
Return a concise markdown summary.
""";

        var user = $"""
Summarize the following OpenAPI specification.
Include: title, version, purpose, authentication schemes, and key endpoints grouped by tag.

JSON:
{specJson}
""";

        return new AiPrompt(system, user);
    }
}
