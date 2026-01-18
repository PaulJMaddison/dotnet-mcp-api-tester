using System.Text.Json.Serialization;

namespace ApiTester.McpServer.Models;

public sealed record MembershipRecord
{
    public Guid OrganisationId { get; init; }
    public Guid UserId { get; init; }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public OrgRole Role { get; init; }

    public DateTime CreatedUtc { get; init; }

    [JsonConstructor]
    public MembershipRecord(Guid organisationId, Guid userId, OrgRole role, DateTime createdUtc)
    {
        OrganisationId = organisationId;
        UserId = userId;
        Role = role;
        CreatedUtc = createdUtc;
    }
}
