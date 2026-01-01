namespace ApiTester.Web.Contracts;

public sealed record RunAnnotationCreateRequest(string? Note, string? JiraLink);

public sealed record RunAnnotationUpdateRequest(string? Note, string? JiraLink);
