using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;

Console.WriteLine("Workshop demo, MCP + RAG");
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
    // MCP tools/call returns result which may contain isError/content
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

    // Critical: clone before JsonDocument is disposed
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

    var m = Regex.Match(text, @"[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}");
    if (!m.Success)
        throw new InvalidOperationException("Could not find a GUID in tool text: " + text);

    return Guid.Parse(m.Value);
}

static void PrintStep(string title, JsonElement result)
{
    Console.WriteLine(title);
    Console.WriteLine(ExtractText(result));
    Console.WriteLine();
}

static void EnsureOkIfPresent(JsonElement result, string stepName)
{
    // Some tools return structured JSON, others return JSON inside text.
    // If it contains ok:false we fail fast with readable output.
    var text = ExtractText(result);

    if (text.Contains("\"ok\": false", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("'ok': false", StringComparison.OrdinalIgnoreCase) ||
        text.Contains("ok\":false", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException($"Step '{stepName}' returned ok:false. Full response:\n{text}");
    }
}

// Give the server a moment to start
await Task.Delay(350);

Console.WriteLine("0) tools/list");
var tools = await ListToolsAsync();
foreach (var t in tools.OrderBy(x => x))
    Console.WriteLine($"- {t}");
Console.WriteLine();

Require(tools, "api_ping");
Require(tools, "api_create_project");
Require(tools, "api_set_current_project");
Require(tools, "api_import_open_api");
Require(tools, "api_rag_index_project");
Require(tools, "api_rag_ask");
// eval is optional

// ---- 1) Ping
var ping = await CallToolAsync("api_ping", new { });
PrintStep("1) Ping", ping);
EnsureOkIfPresent(ping, "Ping");

// ---- 2) Create project
var created = await CallToolAsync("api_create_project", new { name = "Workshop Project" });
PrintStep("2) Create project", created);
EnsureOkIfPresent(created, "Create project");

var projectId = ExtractGuidFromResult(created);
Console.WriteLine($"[demo] Using projectId: {projectId}");
Console.WriteLine();

// ---- 2b) Set current project (string expected by server tool)
var setCurrent = await CallToolAsync("api_set_current_project", new { projectId = projectId.ToString() });
PrintStep("2b) Set current project", setCurrent);
EnsureOkIfPresent(setCurrent, "Set current project");

// ---- 3) Import OpenAPI (tool expects specUrlOrPath, so write to temp file)
var specJson = """
{
  "openapi": "3.0.1",
  "info": { "title": "WeatherForecast API", "version": "1.0" },
  "paths": {
    "/weatherforecast": {
      "get": {
        "operationId": "GetWeatherForecast",
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


var tempSpecPath = Path.Combine(Path.GetTempPath(), $"demo-openapi-{Guid.NewGuid():N}.json");
await File.WriteAllTextAsync(tempSpecPath, specJson);

Console.WriteLine($"[demo] Wrote OpenAPI to: {tempSpecPath}");
Console.WriteLine();

var imported = await CallToolAsync("api_import_open_api", new { specUrlOrPath = tempSpecPath });
PrintStep("3) Import OpenAPI", imported);
EnsureOkIfPresent(imported, "Import OpenAPI");

// ---- 4) RAG index (pass projectId explicitly, so demo is stateless and reliable)
var indexed = await CallToolAsync("api_rag_index_project", new { projectId = projectId.ToString() });
PrintStep("4) RAG index", indexed);
EnsureOkIfPresent(indexed, "RAG index");

// ---- 5) RAG ask (pass projectId explicitly)
var asked = await CallToolAsync("api_rag_ask", new
{
    question =
        "I’m integrating this WeatherForecast API into a .NET 8 service. " +
        "What endpoint do I call, what does it return, and can you show a curl example plus a C# HttpClient example? " +
        "Also, does the spec show any authentication requirements?",
    topK = 10,
    projectId = projectId.ToString()
});

PrintStep("5) RAG ask", asked);
EnsureOkIfPresent(asked, "RAG ask");

// ---- 6) Eval run (optional, pass projectId if your tool supports it)
if (tools.Contains("api_eval_run", StringComparer.OrdinalIgnoreCase))
{
    // If you updated ApiEvalRun to accept projectId, this will work.
    // If not, you can change args to new { }.
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
    // ignore
}

try
{
    File.Delete(tempSpecPath);
}
catch
{
    // ignore
}
