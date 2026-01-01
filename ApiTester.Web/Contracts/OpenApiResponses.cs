namespace ApiTester.Web.Contracts;

public sealed record OpenApiSpecMetadataDto(
    Guid SpecId,
    Guid ProjectId,
    string Title,
    string Version,
    DateTime CreatedUtc);

public sealed record OpenApiSpecDetailDto(
    Guid SpecId,
    Guid ProjectId,
    string Title,
    string Version,
    string SpecJson,
    string SpecHash,
    DateTime CreatedUtc);

public sealed record OpenApiDiffItemDto(
    string Classification,
    string Change,
    string? Path,
    string? Method,
    string Detail);

public sealed record OpenApiDiffResponse(
    Guid SpecAId,
    Guid SpecBId,
    IReadOnlyList<OpenApiDiffItemDto> Differences);
