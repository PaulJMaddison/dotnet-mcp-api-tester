using System.Text.Json;

namespace ApiTester.Web.AI;

public static class AiJsonSchemas
{
    public const string SchemaJson = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["insights"],
  "properties": {
    "insights": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["type", "payload"],
        "properties": {
          "type": { "type": "string" },
          "payload": { "type": "object" }
        }
      }
    }
  }
}
""";

    public static IReadOnlyList<AiInsightPayload> ParseInsights(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new AiSchemaValidationException("AI response must be a JSON object.");

        if (!root.TryGetProperty("insights", out var insightsElement) || insightsElement.ValueKind != JsonValueKind.Array)
            throw new AiSchemaValidationException("AI response must include an insights array.");

        var list = new List<AiInsightPayload>();
        foreach (var insight in insightsElement.EnumerateArray())
        {
            if (insight.ValueKind != JsonValueKind.Object)
                throw new AiSchemaValidationException("Each insight must be a JSON object.");

            if (!insight.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
                throw new AiSchemaValidationException("Each insight must include a string type.");

            if (!insight.TryGetProperty("payload", out var payloadElement) || payloadElement.ValueKind != JsonValueKind.Object)
                throw new AiSchemaValidationException("Each insight must include a payload object.");

            var type = typeElement.GetString() ?? string.Empty;
            var payload = payloadElement.GetRawText();

            ValidatePayload(type, payloadElement);
            list.Add(new AiInsightPayload(type, payload));
        }

        return list;
    }

    private static void ValidatePayload(string type, JsonElement payload)
    {
        if (string.IsNullOrWhiteSpace(type))
            throw new AiSchemaValidationException("Insight type cannot be empty.");

        switch (type.Trim().ToLowerInvariant())
        {
            case "summary":
                RequireString(payload, "text");
                break;
            case "recommendation":
                RequireString(payload, "title");
                RequireString(payload, "detail");
                break;
            case "risk":
                RequireString(payload, "title");
                RequireString(payload, "detail");
                RequireEnum(payload, "severity", ["low", "medium", "high"]);
                break;
            default:
                throw new AiSchemaValidationException($"Unsupported insight type '{type}'.");
        }
    }

    private static void RequireString(JsonElement payload, string propertyName)
    {
        if (!payload.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.String)
            throw new AiSchemaValidationException($"Payload must include string property '{propertyName}'.");
    }

    private static void RequireEnum(JsonElement payload, string propertyName, string[] allowed)
    {
        RequireString(payload, propertyName);
        var value = payload.GetProperty(propertyName).GetString() ?? string.Empty;
        if (!allowed.Contains(value, StringComparer.OrdinalIgnoreCase))
            throw new AiSchemaValidationException($"Payload property '{propertyName}' must be one of: {string.Join(", ", allowed)}.");
    }
}

public sealed record AiInsightPayload(string Type, string JsonPayload);
