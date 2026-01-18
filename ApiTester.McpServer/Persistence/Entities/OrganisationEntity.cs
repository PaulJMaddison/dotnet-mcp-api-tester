namespace ApiTester.McpServer.Persistence.Entities;

public sealed class OrganisationEntity
{
    public Guid OrganisationId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; }
    public int? RetentionDays { get; set; }
    public string? RedactionRulesJson { get; set; }

    public List<ProjectEntity> Projects { get; set; } = new();
    public List<TestRunEntity> Runs { get; set; } = new();
    public List<MembershipEntity> Memberships { get; set; } = new();
    public List<AuditEventEntity> AuditEvents { get; set; } = new();
}
