namespace ApiTester.McpServer.Persistence.Entities;

public sealed class UserEntity
{
    public Guid UserId { get; set; }
    public string ExternalId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime CreatedUtc { get; set; }

    public List<MembershipEntity> Memberships { get; set; } = new();
}
