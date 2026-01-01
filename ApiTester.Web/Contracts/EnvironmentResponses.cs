namespace ApiTester.Web.Contracts;

public sealed record EnvironmentDto(
    Guid EnvironmentId,
    Guid ProjectId,
    string Name,
    string BaseUrl,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);

public sealed record EnvironmentListResponse(IReadOnlyList<EnvironmentDto> Environments);
