namespace ApiTester.McpServer.Models;

public sealed record ProjectRecord(Guid ProjectId, string OwnerKey, string Name, string ProjectKey, DateTime CreatedUtc)
{
    public ProjectRecord(Guid projectId, string name, string projectKey, DateTime createdUtc)
        : this(projectId, OwnerKeyDefaults.Default, name, projectKey, createdUtc)
    {
    }
}
