using System.Diagnostics;
using System.Text.Json;

static async Task<int> Main()
{
    var serverProjectPath = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory, "..", "..", "..", "..", "ApiTester.McpServer", "ApiTester.McpServer.csproj"));

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

    // 3) tools/call: api_import_open_api (Petstore)
    var import = await CallToolAsync("api_import_open_api", new
    {
        specUrlOrPath = "https://petstore3.swagger.io/api/v3/openapi.json"
    });
    PrintToolTextOrRaw("api_import_open_api response:", import);

    // 4) tools/call: api_set_base_url (Petstore)
    var setBaseUrl = await CallToolAsync("api_set_base_url", new
    {
        baseUrl = "https://petstore3.swagger.io/api/v3"
    });
    PrintToolTextOrRaw("api_set_base_url response:", setBaseUrl);

    // -------------------------
    // DAY 5 RUN CHECKS
    // -------------------------

    // Helper: get policy (optional, but useful)
    var getPolicy0 = await CallToolAsync("api_get_policy", new { });
    PrintToolTextOrRaw("api_get_policy (initial) response:", getPolicy0);

    // A) Deny-by-default should block
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
        operationId = "getInventory"
    });
    PrintToolTextOrRaw("A) api_call_operation (should be blocked) response:", callA);

    // B) Allow Petstore explicitly (should attempt and return status, Petstore may be flaky)
    var policyJsonB = """
{
  "dryRun": false,
  "allowedMethods": ["GET"],
  "allowedBaseUrls": ["https://petstore3.swagger.io/api/v3"],
  "blockLocalhost": true,
  "blockPrivateNetworks": true,
  "timeoutSeconds": 10,
  "maxRequestBodyBytes": 262144,
  "maxResponseBodyBytes": 524288
}
""";

    var setPolicyB = await CallToolAsync("api_set_policy", new { policyJson = policyJsonB });
    PrintToolTextOrRaw("api_set_policy B (allow Petstore) response:", setPolicyB);

    var callB1 = await CallToolAsync("api_call_operation", new
    {
        operationId = "getInventory"
    });
    PrintToolTextOrRaw("B1) api_call_operation getInventory (Petstore may 500) response:", callB1);

    var callB2 = await CallToolAsync("api_call_operation", new
    {
        operationId = "getOrderById",
        pathParamsJson = "{\"orderId\":\"1\"}"
    });
    PrintToolTextOrRaw("B2) api_call_operation getOrderById (Petstore may 500) response:", callB2);

    // C) Metadata/link-local SSRF block test.
    // We deliberately allowlist the base URL to prove SSRF guard still blocks it.
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

    // This operation will attempt to build a URL under the metadata base URL.
    // Even if the path doesn't exist, SSRF guard should block before any request is sent.
    var callC = await CallToolAsync("api_call_operation", new
    {
        operationId = "getInventory"
    });
    PrintToolTextOrRaw("C) api_call_operation (should be blocked by SSRF guard) response:", callC);

    // ---- RESET POLICY TO SAFE DEFAULTS ----

    var resetPolicy = await CallToolAsync("api_set_policy", new
    {
                policyJson = """
        {
          "dryRun": true,
          "allowedBaseUrls": [],
          "allowedMethods": ["GET"],
          "timeoutSeconds": 10,
          "maxRequestBodyBytes": 262144,
          "maxResponseBodyBytes": 524288,
          "blockLocalhost": true,
          "blockPrivateNetworks": true
        }
        """
    });

    PrintToolTextOrRaw("api_set_policy (reset to safe defaults) response:", resetPolicy);
    var getPolicyFinal = await CallToolAsync("api_get_policy", new { });
    PrintToolTextOrRaw("api_get_policy (final) response:", getPolicyFinal);


    // Clean shutdown
    try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }

    return 0;
}

return await Main();
