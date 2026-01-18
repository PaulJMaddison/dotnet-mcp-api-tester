using System.Text.Json;

namespace ApiTester.Web.AI;

public static class AiExplainSchemas
{
    public const string SchemaJson = """
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "type": "object",
  "required": ["summary", "inputs", "outputs", "auth", "gotchas", "examples", "markdown"],
  "properties": {
    "summary": { "type": "string" },
    "inputs": { "type": "string" },
    "outputs": { "type": "string" },
    "auth": { "type": "string" },
    "gotchas": {
      "type": "array",
      "items": { "type": "string" }
    },
    "examples": {
      "type": "array",
      "items": {
        "type": "object",
        "required": ["title", "content"],
        "properties": {
          "title": { "type": "string" },
          "content": { "type": "string" }
        }
      }
    },
    "markdown": { "type": "string" }
  }
}
""";

    public static AiExplainPayload ParseExplain(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new AiSchemaValidationException("AI response must be a JSON object.");

        var summary = RequireString(root, "summary");
        var inputs = RequireString(root, "inputs");
        var outputs = RequireString(root, "outputs");
        var auth = RequireString(root, "auth");
        var markdown = RequireString(root, "markdown");

        var gotchasElement = RequireArray(root, "gotchas");
        var gotchas = new List<string>();
        foreach (var gotcha in gotchasElement.EnumerateArray())
        {
            if (gotcha.ValueKind != JsonValueKind.String)
                throw new AiSchemaValidationException("Gotchas must be an array of strings.");
            gotchas.Add(gotcha.GetString() ?? string.Empty);
        }

        var examplesElement = RequireArray(root, "examples");
        var examples = new List<AiExplainExample>();
        foreach (var example in examplesElement.EnumerateArray())
        {
            if (example.ValueKind != JsonValueKind.Object)
                throw new AiSchemaValidationException("Examples must be objects.");

            var title = RequireString(example, "title");
            var content = RequireString(example, "content");
            examples.Add(new AiExplainExample(title, content));
        }

        return new AiExplainPayload(
            summary,
            inputs,
            outputs,
            auth,
            gotchas,
            examples,
            markdown);
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
}

public sealed record AiExplainPayload(
    string Summary,
    string Inputs,
    string Outputs,
    string Auth,
    IReadOnlyList<string> Gotchas,
    IReadOnlyList<AiExplainExample> Examples,
    string Markdown);

public sealed record AiExplainExample(string Title, string Content);
