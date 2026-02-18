using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ApiTester.Cli;

public static class CliApp
{
    public static async Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr, CancellationToken ct)
    {
        try
        {
            if (!CliParser.TryParse(args, out var options, out var error))
            {
                await stderr.WriteLineAsync(error);
                return 2;
            }

            using var client = CreateClient(options!);
            return await ExecuteCommandAsync(client, options!, stdout, stderr, ct);
        }
        catch (InvalidOperationException ex)
        {
            await stderr.WriteLineAsync(ex.Message);
            await stderr.WriteLineAsync(CliParser.Usage());
            return 2;
        }
        catch (Exception ex)
        {
            await stderr.WriteLineAsync($"Command failed: {ex.Message}");
            return 1;
        }
    }

    internal static HttpClient CreateClient(CliOptions options)
    {
        var client = new HttpClient
        {
            BaseAddress = options.BaseUrl
        };

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        return client;
    }

    public static async Task<int> ExecuteCommandAsync(HttpClient client, CliOptions options, TextWriter stdout, TextWriter stderr, CancellationToken ct)
    {
        switch (options.Command)
        {
            case CliCommand.ProjectsList:
            {
                var payload = await client.GetFromJsonAsync<JsonElement>("/api/projects?take=100", ct);
                if (payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty("projects", out var projects))
                {
                    foreach (var item in projects.EnumerateArray())
                    {
                        var id = item.TryGetProperty("projectId", out var idEl) ? idEl.GetString() : string.Empty;
                        var key = item.TryGetProperty("projectKey", out var keyEl) ? keyEl.GetString() : string.Empty;
                        var name = item.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : string.Empty;
                        await stdout.WriteLineAsync($"{id}\t{key}\t{name}");
                    }
                }
                else
                {
                    await stdout.WriteLineAsync(payload.GetRawText());
                }

                return 0;
            }
            case CliCommand.RunExecute execute:
            {
                using var response = await client.PostAsync($"/api/projects/{execute.ProjectId}/runs/execute/{Uri.EscapeDataString(execute.OperationId)}", content: null, ct);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync(ct);
                await stdout.WriteLineAsync(body);
                return 0;
            }
            case CliCommand.RunReport report:
            {
                if (report.Format == "md")
                {
                    using var response = await client.GetAsync($"/api/runs/{report.RunId}/report?format=md", ct);
                    response.EnsureSuccessStatusCode();
                    await stdout.WriteLineAsync(await response.Content.ReadAsStringAsync(ct));
                    return 0;
                }

                using (var response = await client.GetAsync($"/api/runs/{report.RunId}", ct))
                {
                    response.EnsureSuccessStatusCode();
                    await stdout.WriteLineAsync(await response.Content.ReadAsStringAsync(ct));
                }

                return 0;
            }
            case CliCommand.RunEvidencePack evidencePack:
            {
                using var response = await client.GetAsync($"/runs/{evidencePack.RunId}/export/evidence-bundle", ct);
                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync(ct);
                var fullPath = Path.GetFullPath(evidencePack.OutputPath);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                await File.WriteAllBytesAsync(fullPath, bytes, ct);
                await stdout.WriteLineAsync(fullPath);
                return 0;
            }
            default:
                await stderr.WriteLineAsync(CliParser.Usage());
                return 2;
        }
    }
}
