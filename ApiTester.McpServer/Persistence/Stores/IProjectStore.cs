namespace ApiTester.McpServer.Persistence.Stores;

public interface IProjectStore
{
    Task<Guid> CreateAsync(string name, CancellationToken ct);
    Task<object> ListAsync(int take, CancellationToken ct);
    Task<bool> ExistsAsync(Guid projectId, CancellationToken ct);
}
