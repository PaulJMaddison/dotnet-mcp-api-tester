namespace ApiTester.Web.Contracts;

public sealed record EvidenceManifestFile(string Path, string Sha256);

public sealed record EvidenceManifest(DateTime CreatedUtc, IReadOnlyList<EvidenceManifestFile> Files);

public sealed record RunEvidenceAuditResponse(
    Guid RunId,
    DateTimeOffset StartedUtc,
    DateTimeOffset CompletedUtc,
    string ProjectKey,
    string OperationId,
    DateTime CreatedUtc,
    string ImmutableSha256,
    IReadOnlyList<AuditEventResponse> Events);
