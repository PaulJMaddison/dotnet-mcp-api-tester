using System.Diagnostics;
using System.Text;
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
    if (toolsList.RootElement.TryGetProperty("result", out var result) &&
        result.TryGetProperty("tools", out var toolsEl) &&
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

    // 3) tools/call: api_import_open_api
    id++;
    var import = await SendAsync(new
    {
        jsonrpc = "2.0",
        id,
        method = "tools/call",
        @params = new
        {
            name = "api_import_open_api",
            arguments = new
            {
                specUrlOrPath = "https://petstore3.swagger.io/api/v3/openapi.json"
            }
        }
    });

    Console.WriteLine("\napi_import_open_api response:");
    Console.WriteLine(import.RootElement);Console.WriteLine(import.RootElement.GetProperty("result").GetProperty("content")[0].GetProperty("text").GetString());

    // 4) tools/call: api_list_operations
    id++;
    var list = await SendAsync(new
    {
        jsonrpc = "2.0",
        id,
        method = "tools/call",
        @params = new
        {
            name = "api_list_operations",
            arguments = new { }
        }
    });
    Console.WriteLine("\napi_list_operations response:");

    if (list.RootElement.TryGetProperty("result", out var listResult) &&
        listResult.TryGetProperty("content", out var listContent) &&
        listContent.ValueKind == JsonValueKind.Array &&
        listContent.GetArrayLength() > 0 &&
        listContent[0].TryGetProperty("text", out var listText))
    {
        Console.WriteLine(listText.GetString());
    }
    else
    {
        Console.WriteLine(list.RootElement);
    }


    // Clean shutdown
    try { proc.Kill(entireProcessTree: true); } catch { /* ignore */ }

    return 0;
}

return await Main();

