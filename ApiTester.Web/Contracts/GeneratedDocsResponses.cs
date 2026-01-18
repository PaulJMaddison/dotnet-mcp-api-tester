namespace ApiTester.Web.Contracts;

public sealed record GeneratedDocsExampleDto(
    string Title,
    Guid RunId,
    string CaseName,
    int? StatusCode,
    string ResponseSnippet);

public sealed record GeneratedDocsSectionDto(
    string OperationId,
    string Method,
    string Path,
    string Title,
    string Summary,
    string Markdown,
    IReadOnlyList<GeneratedDocsExampleDto> Examples);

public sealed record GeneratedDocsResponse(
    Guid ProjectId,
    Guid SpecId,
    string Title,
    string Summary,
    IReadOnlyList<GeneratedDocsSectionDto> Sections,
    string Markdown,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
