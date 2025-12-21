using System.Diagnostics;
using System.Text.Json;

static async Task<int> Main()
{
    var serverProjectPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "ApiTester.McpServer", "ApiTester.McpServer.csproj"));

    // Local spec path (committed in repo)
    var httpbinSpecPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "Specs", "httpbin.openapi.json"));

    var psi = new ProcessStartInfo
    {
        FileName = "dotnet",
        Arguments = $"run --project \"{serverProjectPath}\"",
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false,
        CreateNoWindow = true,
        WorkingDirectory = Path.GetDirectoryName(serverProjectPath)! // makes server content root sane
    };

    using var proc = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start MCP server process.");

    // Show server logs (stderr) to help debugging
    _ = Task.Run(async () =>
    {
        while (!proc.StandardError.EndOfStream)
        {
            var line = await proc.StandardError.ReadLineAsync();
            if (!string.IsNullOrWhiteSpace(line))
                Console.Error.WriteLine("[server] " + line);
        }
    });

    using var input = proc.StandardInput;
    using var output = proc.StandardOutput;

    var id = 0;

    async Task<JsonDocument> SendAsync(object payload)
    {
        var json = JsonSerializer.Serialize(payload);
        await input.WriteLineAsync(json);
        await input.FlushAsync();

        // MCP stdio transport is line-delimited JSON
        var line = await output.ReadLineAsync();
        if (line is null)
            throw new InvalidOperationException("Server closed stdout unexpectedly.");

        return JsonDocument.Parse(line);
    }

    async Task<JsonDocument> CallToolAsync(string name, object arguments)
    {
        id++;
        return await SendAsync(new
        {
            jsonrpc = "2.0",
            id,
            method = "tools/call",
            @params = new
            {
                name,
                arguments
            }
        });
    }

    static void PrintToolTextOrRaw(string label, JsonDocument response)
    {
        Console.WriteLine($"\n{label}");

        if (response.RootElement.TryGetProperty("result", out var result) &&
            result.TryGetProperty("content", out var content) &&
            content.ValueKind == JsonValueKind.Array &&
            content.GetArrayLength() > 0 &&
            content[0].TryGetProperty("text", out var textEl))
        {
            Console.WriteLine(textEl.GetString());
            return;
        }

        Console.WriteLine(response.RootElement);
    }

    static bool ToolCallIsError(JsonDocument response)
    {
        return response.RootElement.TryGetProperty("result", out var r) &&
               r.TryGetProperty("isError", out var isError) &&
               isError.ValueKind == JsonValueKind.True;
    }

    // 1) initialize
    id++;
    var initResponse = await SendAsync(new
    {
        jsonrpc = "2.0",
        id,
        method = "initialize",
        @params = new
        {
            protocolVersion = "2024-11-05",
            capabilities = new { },
            clientInfo = new { name = "ApiTester.McpClient", version = "0.1.0" }
        }
    });

    Console.WriteLine("initialize response:");
    Console.WriteLine(initResponse.RootElement);

    // 2) tools/list
    id++;
    var toolsList = await SendAsync(new
    {
        jsonrpc = "2.0",
        id,
        method = "tools/list",
        @params = new { }
    });

    Console.WriteLine("\nTool names:");
    if (toolsList.RootElement.TryGetProperty("result", out var tlResult) &&
        tlResult.TryGetProperty("tools", out var toolsEl) &&
        toolsEl.ValueKind == JsonValueKind.Array)
    {
        foreach (var t in toolsEl.EnumerateArray())
        {
            var name = t.GetProperty("name").GetString();
            Console.WriteLine("- " + name);
        }
    }

    Console.WriteLine("\ntools/list response:");
    Console.WriteLine(toolsList.RootElement);

    // 3) Import OpenAPI (local, valid OAS3)
    var import = await CallToolAsync("api_import_open_api", new
    {
        specUrlOrPath = httpbinSpecPath
    });
    PrintToolTextOrRaw("api_import_open_api response:", import);

    // STOP if OpenAPI import failed (prevents cascading errors)
    if (ToolCallIsError(import))
    {
        Console.WriteLine("OpenAPI import failed, stopping client run.");
        return 1;
    }

    // 4) Set base URL (httpbin is reliable)
    var setBaseUrl = await CallToolAsync("api_set_base_url", new
    {
        baseUrl = "https://httpbin.org"
    });
    PrintToolTextOrRaw("api_set_base_url response:", setBaseUrl);

    // Helper: get policy (optional, but useful)
    var getPolicy0 = await CallToolAsync("api_get_policy", new { });
    PrintToolTextOrRaw("api_get_policy (initial) response:", getPolicy0);

    // A) Deny-by-default should block (allowedBaseUrls empty + dryRun=false)
    var policyJsonA = """
{
  "dryRun": false,
  "allowedMethods": ["GET"],
  "allowedBaseUrls": []
}
""";

    var setPolicyA = await CallToolAsync("api_set_policy", new { policyJson = policyJsonA });
    PrintToolTextOrRaw("api_set_policy A (deny-by-default) response:", setPolicyA);

    var callA = await CallToolAsync("api_call_operation", new
    {
        operationId = "getUuid"
    });
    PrintToolTextOrRaw("A) api_call_operation (should be blocked) response:", callA);

    // B) Allow httpbin explicitly (should succeed and return JSON)
    var policyJsonB = """
{
  "dryRun": false,
  "allowedMethods": ["GET"],
  "allowedBaseUrls": ["https://httpbin.org"],
  "blockLocalhost": true,
  "blockPrivateNetworks": true,
  "timeoutSeconds": 10,
  "maxRequestBodyBytes": 262144,
  "maxResponseBodyBytes": 524288
}
""";

    var setPolicyB = await CallToolAsync("api_set_policy", new { policyJson = policyJsonB });
    PrintToolTextOrRaw("api_set_policy B (allow httpbin) response:", setPolicyB);

    var callB1 = await CallToolAsync("api_call_operation", new
    {
        operationId = "getUuid"
    });
    PrintToolTextOrRaw("B1) api_call_operation getUuid response:", callB1);

    var callB2 = await CallToolAsync("api_call_operation", new
    {
        operationId = "getGet"
    });
    PrintToolTextOrRaw("B2) api_call_operation getGet response:", callB2);

    var callB3 = await CallToolAsync("api_call_operation", new
    {
        operationId = "getStatus",
        pathParamsJson = "{\"code\":200}"
    });
    PrintToolTextOrRaw("B3) api_call_operation getStatus(200) response:", callB3);

    // C) Metadata/link-local SSRF block test.
    // Deliberately allowlist the base URL to prove SSRF guard still blocks it.
    var setBaseUrlMeta = await CallToolAsync("api_set_base_url", new
    {
        baseUrl = "http://169.254.169.254"
    });
    PrintToolTextOrRaw("api_set_base_url (metadata IP) response:", setBaseUrlMeta);

    var policyJsonC = """
{
  "dryRun": false,
  "allowedMethods": ["GET"],
  "allowedBaseUrls": ["http://169.254.169.254"],
  "blockLocalhost": true,
  "blockPrivateNetworks": true
}
""";

    var setPolicyC = await CallToolAsync("api_set_policy", new { policyJson = policyJsonC });
    PrintToolTextOrRaw("api_set_policy C (allowlist metadata, expect SSRF block) response:", setPolicyC);

    // Any operation will do, we just want SSRF guard to block before request
    var callC = await CallToolAsync("api_call_operation", new
    {
        operationId = "getUuid"
    });
    PrintToolTextOrRaw("C) api_call_operation (should be blocked by SSRF guard) response:", callC);

    // Day 7 cleanup: one-shot reset (base url + auth + policy) so demos are tidy
    var resetRuntime = await CallToolAsync("api_reset_runtime", new { });
    PrintToolTextOrRaw("api_reset_runtime response:", resetRuntime);

    // -------------------------
    // DAY 8: assert reset_runtime worked
    // -------------------------

    var getPolicyAfterReset = await CallToolAsync("api_get_policy", new { });
    PrintToolTextOrRaw("api_get_policy (after reset_runtime) response:", getPolicyAfterReset);

    // Optional but good: prove we can re-apply policy cleanly after reset and calls work again
    var setPolicyAfterReset = await CallToolAsync("api_set_policy", new { policyJson = policyJsonB });
    PrintToolTextOrRaw("api_set_policy (after reset_runtime, allow httpbin) response:", setPolicyAfterReset);

    var callAfterReset = await CallToolAsync("api_call_operation", new
    {
        operationId = "getUuid"
    });
    PrintToolTextOrRaw("api_call_operation (after reset_runtime + allow httpbin) response:", callAfterReset);

    // Clean shutdown
    try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }

    return 0;
}

return await Main();
