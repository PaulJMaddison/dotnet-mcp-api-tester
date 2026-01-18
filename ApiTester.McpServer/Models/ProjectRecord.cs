using System.Text.Json.Serialization;

namespace ApiTester.McpServer.Models;

public sealed record ProjectRecord
{
    public Guid ProjectId { get; init; }
    public Guid OrganisationId { get; init; } = OrgDefaults.DefaultOrganisationId;
    public string OwnerKey { get; init; } = OwnerKeyDefaults.Default;
    public string Name { get; init; } = string.Empty;
    public string ProjectKey { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }

    // This is the constructor System.Text.Json will use.
    // Note: ownerKey is nullable to tolerate older persisted files.
    [JsonConstructor]
    public ProjectRecord(Guid projectId, Guid organisationId, string? ownerKey, string name, string projectKey, DateTime createdUtc)
    {
        ProjectId = projectId;
        OrganisationId = organisationId != Guid.Empty
            ? organisationId
            : OrgDefaults.DefaultOrganisationId;
        OwnerKey = string.IsNullOrWhiteSpace(ownerKey) ? OwnerKeyDefaults.Default : ownerKey;
        Name = name;
        ProjectKey = projectKey;
        CreatedUtc = createdUtc;
    }

    // Convenience ctor used by code when you don't care about OwnerKey.
    public ProjectRecord(Guid projectId, Guid organisationId, string name, string projectKey, DateTime createdUtc)
        : this(projectId, organisationId, OwnerKeyDefaults.Default, name, projectKey, createdUtc)
    {
    }
}
