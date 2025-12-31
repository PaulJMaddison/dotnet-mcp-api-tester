namespace ApiTester.Web.Contracts;

public sealed record ProjectResponse(Guid ProjectId, string Name, string ProjectKey, DateTime CreatedUtc);

public sealed record ProjectCreateResponse(Guid ProjectId, string Name, DateTime CreatedUtc);

public sealed record ProjectListResponse(int Take, int Total, IReadOnlyList<ProjectResponse> Projects);
