using System.Text;
using System.Text.Json;

namespace ApiTester.Web.AI;

public static class AiDocsSchemas
{
    public const string SchemaJson = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["title", "summary", "sections"],
  "properties": {
    "title": { "type": "string" },
    "summary": { "type": "string" },
    "sections": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["operationId", "method", "path", "title", "summary", "markdown", "examples"],
        "properties": {
          "operationId": { "type": "string" },
          "method": { "type": "string" },
          "path": { "type": "string" },
          "title": { "type": "string" },
          "summary": { "type": "string" },
          "markdown": { "type": "string" },
          "examples": {
            "type": "array",
            "items": {
              "type": "object",
              "required": ["title", "runId", "caseName", "statusCode", "responseSnippet"],
              "properties": {
                "title": { "type": "string" },
                "runId": { "type": "string" },
                "caseName": { "type": "string" },
                "statusCode": { "type": ["integer", "null"] },
                "responseSnippet": { "type": "string" }
              }
            }
          }
        }
      }
    }
  }
}
""";

    public static AiDocsPayload ParseDocs(string json)
    {
        var normalizedJson = NormalizeJson(json);
        using var doc = JsonDocument.Parse(normalizedJson);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new AiSchemaValidationException("AI response must be a JSON object.");

        var title = RequireString(root, "title");
        var summary = RequireString(root, "summary");

        var sectionsElement = RequireArray(root, "sections");
        var sections = new List<AiDocsSection>();
        foreach (var section in sectionsElement.EnumerateArray())
        {
            if (section.ValueKind != JsonValueKind.Object)
                throw new AiSchemaValidationException("Sections must be objects.");

            var operationId = RequireString(section, "operationId");
            var method = RequireString(section, "method");
            var path = RequireString(section, "path");
            var sectionTitle = RequireString(section, "title");
            var sectionSummary = RequireString(section, "summary");
            var markdown = RequireString(section, "markdown");

            var examplesElement = RequireArray(section, "examples");
            var examples = new List<AiDocsExample>();
            foreach (var example in examplesElement.EnumerateArray())
            {
                if (example.ValueKind != JsonValueKind.Object)
                    throw new AiSchemaValidationException("Examples must be objects.");

                var exampleTitle = RequireString(example, "title");
                var runId = RequireString(example, "runId");
                var caseName = RequireString(example, "caseName");
                var statusCode = ReadOptionalInt(example, "statusCode");
                var responseSnippet = RequireString(example, "responseSnippet");

                examples.Add(new AiDocsExample(exampleTitle, runId, caseName, statusCode, responseSnippet));
            }

            sections.Add(new AiDocsSection(operationId, method, path, sectionTitle, sectionSummary, markdown, examples));
        }

        return new AiDocsPayload(title, summary, sections);
    }

    private static string NormalizeJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return json;

        var trimmed = json.Trim();
        var firstObject = TryExtractFirstJsonObject(trimmed);
        if (!string.IsNullOrWhiteSpace(firstObject))
            trimmed = firstObject;

        var builder = new StringBuilder(trimmed.Length);
        var inString = false;
        var escaped = false;

        foreach (var ch in trimmed)
        {
            if (inString && (ch == '\n' || ch == '\r'))
            {
                builder.Append("\\n");
                escaped = false;
                continue;
            }

            builder.Append(ch);

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == '"')
                inString = !inString;
        }

        return builder.ToString();
    }

    private static string? TryExtractFirstJsonObject(string input)
    {
        var start = input.IndexOf('{');
        if (start < 0)
            return null;

        var depth = 0;
        var inString = false;
        var escaped = false;

        for (var i = start; i < input.Length; i++)
        {
            var ch = input[i];

            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (ch == '\\')
            {
                escaped = true;
                continue;
            }

            if (ch == '"')
            {
                inString = !inString;
                continue;
            }

            if (inString)
                continue;

            if (ch == '{')
                depth++;
            else if (ch == '}')
            {
                depth--;
                if (depth == 0)
                    return input[start..(i + 1)];
            }
        }

        return null;
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

    private static int? ReadOptionalInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var prop))
            throw new AiSchemaValidationException($"Property '{propertyName}' is required.");

        if (prop.ValueKind == JsonValueKind.Null)
            return null;

        if (prop.ValueKind != JsonValueKind.Number || !prop.TryGetInt32(out var value))
            throw new AiSchemaValidationException($"Property '{propertyName}' must be an integer or null.");

        return value;
    }
}

public sealed record AiDocsPayload(
    string Title,
    string Summary,
    IReadOnlyList<AiDocsSection> Sections);

public sealed record AiDocsSection(
    string OperationId,
    string Method,
    string Path,
    string Title,
    string Summary,
    string Markdown,
    IReadOnlyList<AiDocsExample> Examples);

public sealed record AiDocsExample(
    string Title,
    string RunId,
    string CaseName,
    int? StatusCode,
    string ResponseSnippet);
