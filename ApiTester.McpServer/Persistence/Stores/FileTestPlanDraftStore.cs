using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileTestPlanDraftStore : ITestPlanDraftStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileTestPlanDraftStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "test-plan-drafts.json");

    public async Task<TestPlanDraftRecord?> GetAsync(Guid draftId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(p => p.DraftId == draftId);
    }

    public async Task<TestPlanDraftRecord> CreateAsync(Guid projectId, string operationId, string planJson, DateTime createdUtc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("operationId is required.", nameof(operationId));

        var record = new TestPlanDraftRecord(Guid.NewGuid(), projectId, operationId.Trim(), planJson, createdUtc);

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            list.Add(record);
            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<List<TestPlanDraftRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<TestPlanDraftRecord>>(json, JsonDefaults.Default) ?? [];
    }

    private async Task SaveAsync(List<TestPlanDraftRecord> list, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }
}
