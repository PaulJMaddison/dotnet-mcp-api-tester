using System.Text.Json.Serialization;

namespace ApiTester.McpServer.Models;

public sealed record OrganisationRecord
{
    public Guid OrganisationId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public DateTime CreatedUtc { get; init; }

    [JsonConstructor]
    public OrganisationRecord(Guid organisationId, string name, string slug, DateTime createdUtc)
    {
        OrganisationId = organisationId;
        Name = name;
        Slug = slug;
        CreatedUtc = createdUtc;
    }
}
