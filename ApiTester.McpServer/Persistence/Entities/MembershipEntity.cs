using ApiTester.McpServer.Models;

namespace ApiTester.McpServer.Persistence.Entities;

public sealed class MembershipEntity
{
    public Guid OrganisationId { get; set; }
    public OrganisationEntity? Organisation { get; set; }
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }
    public OrgRole Role { get; set; }
    public DateTime CreatedUtc { get; set; }
}
