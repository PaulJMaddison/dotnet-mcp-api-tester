namespace ApiTester.McpServer.Persistence.Entities;

public sealed class ApiKeyEntity
{
    public Guid KeyId { get; set; }
    public Guid OrganisationId { get; set; }
    public OrganisationEntity? Organisation { get; set; }
    public Guid UserId { get; set; }
    public UserEntity? User { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public DateTime? ExpiresUtc { get; set; }
    public DateTime? RevokedUtc { get; set; }
    public DateTime? LastUsedUtc { get; set; }
    public string Hash { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
}
