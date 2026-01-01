using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ApiTester.McpServer.Models;
using ApiTester.McpServer.Serialization;

namespace ApiTester.McpServer.Persistence.Stores;

public sealed class FileOpenApiSpecStore : IOpenApiSpecStore
{
    private readonly AppConfig _cfg;
    private readonly SemaphoreSlim _mutex = new(1, 1);

    public FileOpenApiSpecStore(AppConfig cfg)
    {
        _cfg = cfg;
    }

    private string FilePath => Path.Combine(_cfg.WorkingDirectory, "openapi-specs.json");

    public async Task<OpenApiSpecRecord?> GetAsync(Guid projectId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedUtc)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<OpenApiSpecRecord>> ListAsync(Guid projectId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list
            .Where(s => s.ProjectId == projectId)
            .OrderByDescending(s => s.CreatedUtc)
            .ToList();
    }

    public async Task<OpenApiSpecRecord?> GetByIdAsync(Guid specId, CancellationToken ct)
    {
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(s => s.SpecId == specId);
    }

    public async Task<OpenApiSpecRecord> UpsertAsync(Guid projectId, string title, string version, string specJson, string specHash, DateTime createdUtc, CancellationToken ct)
    {
        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var index = list.FindIndex(s => s.ProjectId == projectId && s.SpecHash == specHash);
            if (index >= 0)
                return list[index];

            var record = new OpenApiSpecRecord(Guid.NewGuid(), projectId, title, version, specJson, specHash, createdUtc);
            list.Add(record);

            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    private async Task<List<OpenApiSpecRecord>> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(FilePath))
            return [];

        var json = await File.ReadAllTextAsync(FilePath, ct);
        if (string.IsNullOrWhiteSpace(json))
            return [];

        var list = JsonSerializer.Deserialize<List<OpenApiSpecRecord>>(json, JsonDefaults.Default) ?? [];
        return list
            .Select(record => string.IsNullOrWhiteSpace(record.SpecHash)
                ? record with { SpecHash = ComputeSpecHash(record.SpecJson) }
                : record)
            .ToList();
    }

    private async Task SaveAsync(List<OpenApiSpecRecord> list, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(list, JsonDefaults.Default);
        await File.WriteAllTextAsync(FilePath, json, ct);
    }

    private static string ComputeSpecHash(string specJson)
    {
        using var sha = SHA256.Create();
        var bytes = Encoding.UTF8.GetBytes(specJson);
        var hash = sha.ComputeHash(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
