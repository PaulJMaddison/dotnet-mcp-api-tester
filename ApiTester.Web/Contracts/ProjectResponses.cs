namespace ApiTester.Web.Contracts;

public sealed record ProjectDto(Guid ProjectId, string Name, string ProjectKey, DateTime CreatedUtc);

public sealed record ProjectListResponse(IReadOnlyList<ProjectDto> Projects, PageMetadata Metadata);
