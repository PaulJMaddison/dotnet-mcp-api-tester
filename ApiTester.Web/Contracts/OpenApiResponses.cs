namespace ApiTester.Web.Contracts;

public sealed record OpenApiSpecMetadataDto(
    Guid ProjectId,
    string Title,
    string Version,
    DateTime CreatedUtc);
