using ApiTester.McpServer.Models;
using Microsoft.Extensions.Hosting;
using System.Text.Json;

namespace ApiTester.McpServer.Services;

public sealed class FileTestRunStore : ITestRunStore
{
    private readonly string _root;
    private readonly JsonSerializerOptions _json;

    public FileTestRunStore(IHostEnvironment env)
    {
        // Persist under content root so it behaves nicely when run from the repo
        _root = Path.Combine(env.ContentRootPath, "RunHistory");
        Directory.CreateDirectory(_root);

        _json = new JsonSerializerOptions
        {
            WriteIndented = true
        };
    }

    public async Task SaveAsync(TestRunRecord record, CancellationToken ct = default)
    {
        var file = GetFile(record.RunId);

        var tmp = file + ".tmp";
        var json = JsonSerializer.Serialize(record, _json);

        await File.WriteAllTextAsync(tmp, json, ct);
        File.Move(tmp, file, overwrite: true);
    }

    public async Task<TestRunRecord?> GetAsync(Guid runId, CancellationToken ct = default)
    {
        var file = GetFile(runId);
        if (!File.Exists(file)) return null;

        var json = await File.ReadAllTextAsync(file, ct);
        return JsonSerializer.Deserialize<TestRunRecord>(json, _json);
    }

    public async Task<IReadOnlyList<TestRunRecord>> ListAsync(int take = 50, CancellationToken ct = default)
    {
        if (!Directory.Exists(_root))
            return Array.Empty<TestRunRecord>();

        var files = Directory
            .EnumerateFiles(_root, "*.json", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(Math.Max(1, take))
            .ToList();

        var results = new List<TestRunRecord>(files.Count);

        foreach (var f in files)
        {
            var json = await File.ReadAllTextAsync(f, ct);
            var rec = JsonSerializer.Deserialize<TestRunRecord>(json, _json);
            if (rec is not null) results.Add(rec);
        }

        return results;
    }

    private string GetFile(Guid runId) => Path.Combine(_root, $"{runId:N}.json");
}
