namespace ApiTester.McpServer.Runtime;

public sealed class ProjectContext
{
    public Guid? CurrentProjectId { get; private set; }

    public void SetCurrentProject(Guid projectId) => CurrentProjectId = projectId;
    public void Clear() => CurrentProjectId = null;
}
