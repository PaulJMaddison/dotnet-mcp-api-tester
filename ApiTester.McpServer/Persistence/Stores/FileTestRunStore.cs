using ApiTester.McpServer.Models;
using System.Text.Json;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileTestRunStore : ITestRunStore
{
    private readonly AppConfig _cfg;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public FileTestRunStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string RootPath => Path.Combine(_cfg.WorkingDirectory, "run-history");

    private string ProjectPath(string projectKey)
        => Path.Combine(RootPath, Sanitize(projectKey));

    private string RunFilePath(string projectKey, Guid runId)
        => Path.Combine(ProjectPath(projectKey), $"{runId:D}.json");

    public async Task SaveAsync(TestRunRecord record)
    {
        var projectKey = string.IsNullOrWhiteSpace(record.ProjectKey) ? "default" : record.ProjectKey.Trim();

        Directory.CreateDirectory(ProjectPath(projectKey));

        var path = RunFilePath(projectKey, record.RunId);
        var json = JsonSerializer.Serialize(record, JsonOptions);

        await File.WriteAllTextAsync(path, json);
    }

    public async Task<TestRunRecord?> GetAsync(Guid runId)
    {
        if (!Directory.Exists(RootPath))
            return null;

        // Search all projects for the runId file
        foreach (var projectDir in Directory.EnumerateDirectories(RootPath))
        {
            var candidate = Path.Combine(projectDir, $"{runId:D}.json");
            if (!File.Exists(candidate))
                continue;

            var json = await File.ReadAllTextAsync(candidate);
            return JsonSerializer.Deserialize<TestRunRecord>(json, JsonOptions);
        }

        return null;
    }

    public async Task<IReadOnlyList<TestRunRecord>> ListAsync(string projectKey, int take, string? operationId = null)
    {
        projectKey = string.IsNullOrWhiteSpace(projectKey) ? "default" : projectKey.Trim();
        operationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();

        var dir = ProjectPath(projectKey);
        if (!Directory.Exists(dir))
            return Array.Empty<TestRunRecord>();

        var files = Directory.EnumerateFiles(dir, "*.json", SearchOption.TopDirectoryOnly)
            .Select(f => new FileInfo(f))
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Take(Math.Max(1, take))
            .ToList();

        var list = new List<TestRunRecord>(files.Count);

        foreach (var fi in files)
        {
            var json = await File.ReadAllTextAsync(fi.FullName);
            var record = JsonSerializer.Deserialize<TestRunRecord>(json, JsonOptions);
            if (record is null)
                continue;

            if (operationId is not null &&
                !string.Equals(record.OperationId, operationId, StringComparison.OrdinalIgnoreCase))
                continue;

            // Ensure ProjectKey is always set even for older records
            if (string.IsNullOrWhiteSpace(record.ProjectKey))
            {
                record = new TestRunRecord
                {
                    RunId = record.RunId,
                    ProjectKey = projectKey,
                    OperationId = record.OperationId,
                    StartedUtc = record.StartedUtc,
                    CompletedUtc = record.CompletedUtc,
                    Result = record.Result
                };
            }

            list.Add(record);
        }

        return list;
    }

    private static string Sanitize(string s)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = s.Where(c => !invalid.Contains(c)).ToArray();
        var cleaned = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "default" : cleaned;
    }
}
