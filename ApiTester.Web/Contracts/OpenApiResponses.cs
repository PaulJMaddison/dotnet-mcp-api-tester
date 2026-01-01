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
