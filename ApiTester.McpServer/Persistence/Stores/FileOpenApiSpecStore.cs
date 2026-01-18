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

    public async Task<OpenApiSpecRecord?> GetAsync(Guid tenantId, Guid projectId, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        var list = await LoadAsync(ct);
        return list
            .Where(s => s.ProjectId == projectId && s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedUtc)
            .FirstOrDefault();
    }

    public async Task<IReadOnlyList<OpenApiSpecRecord>> ListAsync(Guid tenantId, Guid projectId, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        var list = await LoadAsync(ct);
        return list
            .Where(s => s.ProjectId == projectId && s.TenantId == tenantId)
            .OrderByDescending(s => s.CreatedUtc)
            .ToList();
    }

    public async Task<OpenApiSpecRecord?> GetByIdAsync(Guid tenantId, Guid specId, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        var list = await LoadAsync(ct);
        return list.FirstOrDefault(s => s.SpecId == specId && s.TenantId == tenantId);
    }

    public async Task<OpenApiSpecRecord> UpsertAsync(Guid tenantId, Guid projectId, string title, string version, string specJson, string specHash, DateTime createdUtc, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var index = list.FindIndex(s => s.ProjectId == projectId && s.SpecHash == specHash && s.TenantId == tenantId);
            if (index >= 0)
                return list[index];

            var record = new OpenApiSpecRecord(Guid.NewGuid(), projectId, tenantId, title, version, specJson, specHash, createdUtc);
            list.Add(record);

            await SaveAsync(list, ct);
            return record;
        }
        finally
        {
            _mutex.Release();
        }
    }

    public async Task<bool> DeleteAsync(Guid tenantId, Guid specId, CancellationToken ct)
    {
        tenantId = NormalizeTenantId(tenantId);
        await _mutex.WaitAsync(ct);
        try
        {
            var list = await LoadAsync(ct);
            var removed = list.RemoveAll(spec => spec.SpecId == specId && spec.TenantId == tenantId) > 0;
            if (removed)
                await SaveAsync(list, ct);

            return removed;
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
            .Select(NormalizeTenant)
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

    private static OpenApiSpecRecord NormalizeTenant(OpenApiSpecRecord record)
    {
        if (record.TenantId != Guid.Empty)
            return record;

        return record with { TenantId = OrgDefaults.DefaultOrganisationId };
    }

    private static Guid NormalizeTenantId(Guid tenantId)
        => tenantId == Guid.Empty ? OrgDefaults.DefaultOrganisationId : tenantId;
}
