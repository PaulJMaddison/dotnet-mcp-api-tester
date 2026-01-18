namespace ApiTester.McpServer.Models;

public sealed record GeneratedDocsRecord(
    Guid DocsId,
    Guid OrganisationId,
    Guid ProjectId,
    Guid SpecId,
    string DocsJson,
    DateTime CreatedUtc,
    DateTime UpdatedUtc);
