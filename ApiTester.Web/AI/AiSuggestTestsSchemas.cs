using System.Text.Json;

namespace ApiTester.Web.AI;

public static class AiSuggestTestsSchemas
{
    public const string SchemaJson = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["cases"],
  "properties": {
    "cases": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["name", "rationale", "params", "expectedStatusRanges"],
        "properties": {
          "name": { "type": "string" },
          "rationale": { "type": "string" },
          "params": {
            "type": "object",
            "properties": {
              "path": {
                "type": "object",
                "additionalProperties": { "type": "string" }
              },
              "query": {
                "type": "object",
                "additionalProperties": { "type": "string" }
              },
              "headers": {
                "type": "object",
                "additionalProperties": { "type": "string" }
              },
              "bodyJson": { "type": "string" }
            }
          },
          "expectedStatusRanges": {
            "type": "array",
            "items": { "type": "string" }
          }
        }
      }
    }
  }
}
""";

    public static IReadOnlyList<AiSuggestedTestCase> ParseSuggestions(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new AiSchemaValidationException("AI response must be a JSON object.");

        if (!root.TryGetProperty("cases", out var casesElement) || casesElement.ValueKind != JsonValueKind.Array)
            throw new AiSchemaValidationException("AI response must include a cases array.");

        var list = new List<AiSuggestedTestCase>();
        foreach (var testCase in casesElement.EnumerateArray())
        {
            if (testCase.ValueKind != JsonValueKind.Object)
                throw new AiSchemaValidationException("Each test case must be a JSON object.");

            var name = RequireString(testCase, "name");
            var rationale = RequireString(testCase, "rationale");
            var parameters = RequireParams(testCase, "params");
            var ranges = RequireStringArray(testCase, "expectedStatusRanges");

            if (ranges.Count == 0)
                throw new AiSchemaValidationException("Each test case must include at least one expected status range.");

            list.Add(new AiSuggestedTestCase(name, rationale, parameters, ranges));
        }

        return list;
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.String)
            throw new AiSchemaValidationException($"Property '{propertyName}' must be a string.");

        return property.GetString() ?? string.Empty;
    }

    private static IReadOnlyList<string> RequireStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
            throw new AiSchemaValidationException($"Property '{propertyName}' must be an array of strings.");

        var list = new List<string>();
        foreach (var item in property.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new AiSchemaValidationException($"Property '{propertyName}' must contain only strings.");

            list.Add(item.GetString() ?? string.Empty);
        }

        return list;
    }

    private static AiSuggestedParams RequireParams(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Object)
            throw new AiSchemaValidationException($"Property '{propertyName}' must be an object.");

        return new AiSuggestedParams(
            ReadStringMap(property, "path"),
            ReadStringMap(property, "query"),
            ReadStringMap(property, "headers"),
            ReadOptionalString(property, "bodyJson"));
    }

    private static Dictionary<string, string> ReadStringMap(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (property.ValueKind != JsonValueKind.Object)
            throw new AiSchemaValidationException($"Property '{propertyName}' must be an object.");

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in property.EnumerateObject())
        {
            if (entry.Value.ValueKind != JsonValueKind.String)
                throw new AiSchemaValidationException($"Property '{propertyName}' values must be strings.");

            map[entry.Name] = entry.Value.GetString() ?? string.Empty;
        }

        return map;
    }

    private static string? ReadOptionalString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var property))
            return null;

        if (property.ValueKind == JsonValueKind.Null)
            return null;

        if (property.ValueKind != JsonValueKind.String)
            throw new AiSchemaValidationException($"Property '{propertyName}' must be a string.");

        return property.GetString();
    }
}

public sealed record AiSuggestedTestCase(
    string Name,
    string Rationale,
    AiSuggestedParams Params,
    IReadOnlyList<string> ExpectedStatusRanges);

public sealed record AiSuggestedParams(
    Dictionary<string, string> Path,
    Dictionary<string, string> Query,
    Dictionary<string, string> Headers,
    string? BodyJson);
