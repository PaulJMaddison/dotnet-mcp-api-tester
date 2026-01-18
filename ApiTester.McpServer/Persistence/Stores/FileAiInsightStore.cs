using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileAiInsightStore : IAiInsightStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileAiInsightStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "ai-insights.json");

    public async Task<IReadOnlyList<AiInsightRecord>> ListAsync(Guid organisationId, Guid projectId, Guid runId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list
            .Where(i => i.OrganisationId == organisationId && i.ProjectId == projectId && i.RunId == runId)
            .OrderBy(i => i.CreatedUtc)
            .ToList();
    }

    public async Task<IReadOnlyList<AiInsightRecord>> CreateAsync(
        Guid organisationId,
        Guid projectId,
        Guid runId,
        string operationId,
        IReadOnlyList<AiInsightCreate> insights,
        string modelId,
        DateTime createdUtc,
        CancellationToken ct)
    {
        if (insights is null || insights.Count == 0)
            return Array.Empty<AiInsightRecord>();

        operationId = (operationId ?? string.Empty).Trim();
        modelId = (modelId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(operationId))
            throw new ArgumentException("Operation id is required.", nameof(operationId));

        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var records = insights
                .Select(insight => new AiInsightRecord(
                    Guid.NewGuid(),
                    organisationId,
                    projectId,
                    runId,
                    operationId,
                    NormalizeType(insight.Type),
                    NormalizePayload(insight.JsonPayload),
                    modelId,
                    createdUtc))
                .ToList();

            list.AddRange(records);
            await SaveAsync(list, ct);
            return records;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<List<AiInsightRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<List<AiInsightRecord>>(json, JsonDefaults.Default) ?? [];
    }

    private async Task SaveAsync(List<AiInsightRecord> list, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }

    private static string NormalizeType(string? type)
    {
        type = (type ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(type))
            throw new ArgumentException("Insight type is required.", nameof(type));
        return type;
    }

    private static string NormalizePayload(string? payload)
    {
        payload = (payload ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(payload))
            throw new ArgumentException("Insight payload is required.", nameof(payload));
        return payload;
    }
}
