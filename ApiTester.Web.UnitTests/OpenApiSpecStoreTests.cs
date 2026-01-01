using ApiTester.McpServer.Persistence.Stores;

namespace ApiTester.Web.UnitTests;

public sealed class OpenApiSpecStoreTests
{
    [Fact]
    public async Task UpsertAsync_DedupesByHash()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"api-tester-specs-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            var store = new FileOpenApiSpecStore(new AppConfig { WorkingDirectory = tempDir });
            var projectId = Guid.NewGuid();
            var specJson = "{\"openapi\":\"3.0.0\"}";
            var specHash = "abc123";
            var createdUtc = DateTime.UtcNow;

            var first = await store.UpsertAsync(projectId, "Sample", "1.0.0", specJson, specHash, createdUtc, CancellationToken.None);
            var second = await store.UpsertAsync(projectId, "Sample", "1.0.0", specJson, specHash, createdUtc.AddMinutes(5), CancellationToken.None);

            Assert.Equal(first.SpecId, second.SpecId);

            var list = await store.ListAsync(projectId, CancellationToken.None);
            Assert.Single(list);
        }
        finally
        {
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
