namespace ApiTester.McpServer.Runtime;

public sealed class ProjectContext
{
    public Guid? CurrentProjectId { get; private set; }

    public void SetCurrentProject(Guid projectId)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("projectId must not be empty.", nameof(projectId));

        CurrentProjectId = projectId;
    }

    public void Clear() => CurrentProjectId = null;
}
