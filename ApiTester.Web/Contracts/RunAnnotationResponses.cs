namespace ApiTester.Web.Contracts;

public sealed record RunAnnotationDto(
    Guid AnnotationId,
    Guid RunId,
    string Note,
    string? JiraLink,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record RunAnnotationListResponse(Guid RunId, IReadOnlyList<RunAnnotationDto> Annotations);
