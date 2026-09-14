using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

Console.WriteLine("Workshop demo, MCP + grounded RAG");
Console.WriteLine();

var externalSpec = GetArg(args, "--spec");
var externalQuestion = GetArg(args, "--question");
var topK = int.TryParse(GetArg(args, "--top-k"), out var parsedTopK)
    ? Math.Clamp(parsedTopK, 1, 20)
    : 8;

if (!string.IsNullOrWhiteSpace(externalSpec))
    Console.WriteLine($"[demo] Using supplied OpenAPI source: {externalSpec}");
else
    Console.WriteLine("[demo] No --spec supplied; using the built-in deterministic WeatherForecast fixture.");
Console.WriteLine();

var serverProject = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "ApiTester.McpServer"));
var psi = new ProcessStartInfo("dotnet", "run --project ApiTester.McpServer")
{
    WorkingDirectory = Path.GetDirectoryName(serverProject) ?? serverProject,
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    UseShellExecute = false
};

using var proc = Process.Start(psi);
if (proc is null) return;

_ = Task.Run(async () =>
{
    while (!proc.StandardError.EndOfStream)
    {
        var line = await proc.StandardError.ReadLineAsync();
        if (!string.IsNullOrWhiteSpace(line))
            Console.WriteLine($"[server] {line}");
    }
});

async Task<JsonDocument> SendAsync(object payload)
{
    var json = JsonSerializer.Serialize(payload);
    await proc.StandardInput.WriteLineAsync(json);

    var line = await proc.StandardOutput.ReadLineAsync();
    if (line is null)
        throw new InvalidOperationException("No response from server.");

    return JsonDocument.Parse(line);
}

static void ThrowIfRpcError(JsonDocument doc)
{
    if (doc.RootElement.TryGetProperty("error", out var err))
        throw new InvalidOperationException("JSON-RPC error: " + err);
}

static void ThrowIfToolError(JsonDocument doc)
{
    if (doc.RootElement.TryGetProperty("result", out var result) &&
        result.TryGetProperty("isError", out var isError) &&
        isError.ValueKind == JsonValueKind.True)
    {
        var content = result.TryGetProperty("content", out var c) ? c.ToString() : "<no content>";
        throw new InvalidOperationException("Tool error: " + content);
    }
}

async Task<JsonElement> CallToolAsync(string toolName, object args)
{
    using var doc = await SendAsync(new
    {
        jsonrpc = "2.0",
        id = Guid.NewGuid().ToString("N"),
        method = "tools/call",
        @params = new { name = toolName, arguments = args }
    });

    ThrowIfRpcError(doc);
    ThrowIfToolError(doc);
    return doc.RootElement.GetProperty("result").Clone();
}

async Task<List<string>> ListToolsAsync()
{
    using var doc = await SendAsync(new
    {
        jsonrpc = "2.0",
        id = Guid.NewGuid().ToString("N"),
        method = "tools/list",
        @params = new { }
    });

    ThrowIfRpcError(doc);

    return doc.RootElement
        .GetProperty("result")
        .GetProperty("tools")
        .EnumerateArray()
        .Select(t => t.GetProperty("name").GetString()!)
        .ToList();
}

static void Require(IReadOnlyCollection<string> tools, string name)
{
    if (!tools.Contains(name, StringComparer.OrdinalIgnoreCase))
        throw new InvalidOperationException($"Tool '{name}' not found. Check tools/list output.");
}

static string ExtractText(JsonElement toolResult)
{
    if (!toolResult.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        return toolResult.ToString();

    var parts = new List<string>();
    foreach (var item in content.EnumerateArray())
    {
        if (item.TryGetProperty("type", out var t) && t.GetString() == "text" &&
            item.TryGetProperty("text", out var text))
        {
            parts.Add(text.GetString() ?? string.Empty);
        }
    }

    return string.Join(Environment.NewLine, parts);
}

static Guid ExtractGuidFromResult(JsonElement toolResult)
{
    var text = ExtractText(toolResult);
    var match = Regex.Match(text, @"[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}");
    if (!match.Success)
        throw new InvalidOperationException("Could not find a GUID in tool text: " + text);

    return Guid.Parse(match.Value);
}

static void PrintStep(string title, JsonElement result)
{
    Console.WriteLine(title);
    Console.WriteLine(ExtractText(result));
    Console.WriteLine();
}

static void EnsureOkIfPresent(JsonElement result, string stepName)
{
    var text = ExtractText(result);

    if (text.Contains("\"ok\": false", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("'ok': false", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("ok\":false", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Step '{stepName}' returned ok:false. Full response:\n{text}");
    }
}

await Task.Delay(350);

Console.WriteLine("0) tools/list");
var tools = await ListToolsAsync();
foreach (var tool in tools.OrderBy(x => x))
    Console.WriteLine($"- {tool}");
Console.WriteLine();

Require(tools, "api_ping");
Require(tools, "api_create_project");
Require(tools, "api_set_current_project");
Require(tools, "api_import_open_api");
Require(tools, "api_rag_index_project");
Require(tools, "api_rag_ask");

var ping = await CallToolAsync("api_ping", new { });
PrintStep("1) Ping", ping);
EnsureOkIfPresent(ping, "Ping");

var created = await CallToolAsync("api_create_project", new { name = "Grounded API Demo" });
PrintStep("2) Create project", created);
EnsureOkIfPresent(created, "Create project");

var projectId = ExtractGuidFromResult(created);
Console.WriteLine($"[demo] Using projectId: {projectId}");
Console.WriteLine();

var setCurrent = await CallToolAsync("api_set_current_project", new { projectId = projectId.ToString() });
PrintStep("2b) Set current project", setCurrent);
EnsureOkIfPresent(setCurrent, "Set current project");

string? tempSpecPath = null;
var specSource = externalSpec;
if (string.IsNullOrWhiteSpace(specSource))
{
    var specJson = """
    {
      "openapi": "3.0.1",
      "info": { "title": "WeatherForecast API", "version": "1.0" },
      "paths": {
        "/weatherforecast": {
          "get": {
            "operationId": "GetWeatherForecast",
            "summary": "Return the current weather forecast",
            "responses": {
              "200": {
                "description": "OK",
                "content": {
                  "application/json": {
                    "schema": {
                      "type": "array",
                      "items": { "$ref": "#/components/schemas/WeatherForecast" }
                    }
                  }
                }
              }
            }
          }
        }
      },
      "components": {
        "schemas": {
          "WeatherForecast": {
            "type": "object",
            "required": [ "date", "temperatureC", "summary" ],
            "properties": {
              "date": { "type": "string", "format": "date-time" },
              "temperatureC": { "type": "integer", "format": "int32" },
              "temperatureF": { "type": "integer", "format": "int32", "readOnly": true },
              "summary": { "type": "string", "nullable": true }
            }
          }
        }
      }
    }
    """;

    tempSpecPath = Path.Combine(Path.GetTempPath(), $"demo-openapi-{Guid.NewGuid():N}.json");
    await File.WriteAllTextAsync(tempSpecPath, specJson);
    specSource = tempSpecPath;
    Console.WriteLine($"[demo] Wrote built-in OpenAPI fixture to: {tempSpecPath}");
    Console.WriteLine();
}

var imported = await CallToolAsync("api_import_open_api", new { specUrlOrPath = specSource });
PrintStep("3) Import OpenAPI", imported);
EnsureOkIfPresent(imported, "Import OpenAPI");

var indexed = await CallToolAsync("api_rag_index_project", new { projectId = projectId.ToString() });
PrintStep("4) RAG index", indexed);
EnsureOkIfPresent(indexed, "RAG index");

var question = string.IsNullOrWhiteSpace(externalQuestion)
    ? "What are the most important endpoints in this API, how do I call them from .NET 8, what authentication is explicitly documented, and what edge cases should I test? Do not invent anything not present in the supplied API evidence."
    : externalQuestion;

Console.WriteLine($"[demo] Question: {question}");
Console.WriteLine();

var asked = await CallToolAsync("api_rag_ask", new
{
    question,
    topK,
    projectId = projectId.ToString()
});

PrintStep("5) Grounded RAG answer", asked);
EnsureOkIfPresent(asked, "RAG ask");

if (tools.Contains("api_eval_run", StringComparer.OrdinalIgnoreCase))
{
    var eval = await CallToolAsync("api_eval_run", new { projectId = projectId.ToString() });
    PrintStep("6) Eval run", eval);
    EnsureOkIfPresent(eval, "Eval run");
}

try
{
    if (!proc.HasExited)
        proc.Kill(entireProcessTree: true);
}
catch
{
    // Best-effort cleanup only.
}

if (!string.IsNullOrWhiteSpace(tempSpecPath))
{
    try
    {
        File.Delete(tempSpecPath);
    }
    catch
    {
        // Best-effort cleanup only.
    }
}

static string? GetArg(string[] arguments, string name)
{
    for (var i = 0; i < arguments.Length; i++)
    {
        if (!string.Equals(arguments[i], name, StringComparison.OrdinalIgnoreCase))
            continue;

        if (i + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[i + 1]))
            throw new ArgumentException($"{name} requires a value.");

        return arguments[i + 1];
    }

    return null;
}
