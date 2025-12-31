using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileTestPlanStore : ITestPlanStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileTestPlanStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "test-plans.json");

    public async Task<TestPlanRecord?> GetAsync(Guid projectId, string operationId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(p =>
            p.ProjectId == projectId &&
            string.Equals(p.OperationId, operationId, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<TestPlanRecord> UpsertAsync(Guid projectId, string operationId, string planJson, DateTime createdUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("operationId is required.", nameof(operationId));

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var index = list.FindIndex(p =>
                p.ProjectId == projectId &&
                string.Equals(p.OperationId, operationId, StringComparison.OrdinalIgnoreCase));

            var record = new TestPlanRecord(projectId, operationId.Trim(), planJson, createdUtc);
            if (index >= 0)
            {
                list[index] = record;
            }
            else
            {
                list.Add(record);
            }

            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<List<TestPlanRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<TestPlanRecord>>(json, JsonDefaults.Default) ?? [];
    }

    private async Task SaveAsync(List<TestPlanRecord> list, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }
}
