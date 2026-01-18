using System.Text.Json;

namespace ApiTester.Web.AI;

public static class AiRunSummarySchemas
{
    public const string SchemaJson = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": [
    "overallSummary",
    "topFailures",
    "flakeAssessment",
    "regressionLikelihood",
    "recommendedNextActions"
  ],
  "properties": {
    "overallSummary": { "type": "string" },
    "topFailures": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["title", "evidenceRefs"],
        "properties": {
          "title": { "type": "string" },
          "evidenceRefs": {
            "type": "array",
            "items": {
              "type": "object",
              "required": ["caseName"],
              "properties": {
                "caseName": { "type": "string" },
                "failureReason": { "type": "string" }
              }
            }
          }
        }
      }
    },
    "flakeAssessment": { "type": "string" },
    "regressionLikelihood": {
      "type": "object",
      "required": ["level", "rationale"],
      "properties": {
        "level": { "type": "string", "enum": ["low", "medium", "high"] },
        "rationale": { "type": "string" }
      }
    },
    "recommendedNextActions": {
      "type": "array",
      "items": { "type": "string" }
    }
  }
}
""";

    public static AiRunSummaryPayload ParseSummary(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new AiSchemaValidationException("AI response must be a JSON object.");

        var overallSummary = RequireString(root, "overallSummary");
        var flakeAssessment = RequireString(root, "flakeAssessment");

        var regressionEl = RequireObject(root, "regressionLikelihood");
        var regressionLevel = RequireString(regressionEl, "level");
        if (!IsAllowedRegressionLevel(regressionLevel))
            throw new AiSchemaValidationException("regressionLikelihood.level must be one of: low, medium, high.");
        var regressionRationale = RequireString(regressionEl, "rationale");
        var regressionLikelihood = new AiRunSummaryRegressionLikelihood(regressionLevel, regressionRationale);

        var topFailuresEl = RequireArray(root, "topFailures");
        var failures = new List<AiRunSummaryFailure>();
        foreach (var failureEl in topFailuresEl.EnumerateArray())
        {
            if (failureEl.ValueKind != JsonValueKind.Object)
                throw new AiSchemaValidationException("Top failures must be objects.");

            var title = RequireString(failureEl, "title");
            var evidenceEl = RequireArray(failureEl, "evidenceRefs");
            var evidenceRefs = new List<AiRunSummaryEvidenceRef>();
            foreach (var evidence in evidenceEl.EnumerateArray())
            {
                if (evidence.ValueKind != JsonValueKind.Object)
                    throw new AiSchemaValidationException("Evidence refs must be objects.");

                var caseName = RequireString(evidence, "caseName");
                string? failureReason = null;
                if (evidence.TryGetProperty("failureReason", out var reasonEl))
                {
                    if (reasonEl.ValueKind != JsonValueKind.String)
                        throw new AiSchemaValidationException("Evidence ref failureReason must be a string.");
                    failureReason = reasonEl.GetString();
                }

                evidenceRefs.Add(new AiRunSummaryEvidenceRef(caseName, failureReason));
            }

            if (evidenceRefs.Count == 0)
                throw new AiSchemaValidationException("Top failures must include at least one evidenceRef.");

            failures.Add(new AiRunSummaryFailure(title, evidenceRefs));
        }

        var actionsEl = RequireArray(root, "recommendedNextActions");
        var actions = new List<string>();
        foreach (var action in actionsEl.EnumerateArray())
        {
            if (action.ValueKind != JsonValueKind.String)
                throw new AiSchemaValidationException("recommendedNextActions must be an array of strings.");
            actions.Add(action.GetString() ?? string.Empty);
        }

        return new AiRunSummaryPayload(
            overallSummary,
            failures,
            flakeAssessment,
            regressionLikelihood,
            actions);
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.String)
            throw new AiSchemaValidationException($"Property '{propertyName}' must be a string.");
        return prop.GetString() ?? string.Empty;
    }

    private static JsonElement RequireArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Array)
            throw new AiSchemaValidationException($"Property '{propertyName}' must be an array.");
        return prop;
    }

    private static JsonElement RequireObject(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop) || prop.ValueKind != JsonValueKind.Object)
            throw new AiSchemaValidationException($"Property '{propertyName}' must be an object.");
        return prop;
    }

    private static bool IsAllowedRegressionLevel(string value)
        => value.Equals("low", StringComparison.OrdinalIgnoreCase)
           || value.Equals("medium", StringComparison.OrdinalIgnoreCase)
           || value.Equals("high", StringComparison.OrdinalIgnoreCase);
}

public sealed record AiRunSummaryPayload(
    string OverallSummary,
    IReadOnlyList<AiRunSummaryFailure> TopFailures,
    string FlakeAssessment,
    AiRunSummaryRegressionLikelihood RegressionLikelihood,
    IReadOnlyList<string> RecommendedNextActions);

public sealed record AiRunSummaryFailure(
    string Title,
    IReadOnlyList<AiRunSummaryEvidenceRef> EvidenceRefs);

public sealed record AiRunSummaryEvidenceRef(
    string CaseName,
    string? FailureReason);

public sealed record AiRunSummaryRegressionLikelihood(
    string Level,
    string Rationale);
