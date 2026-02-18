namespace ApiTester.Site.Data;

public enum TenantRole
{
    Owner = 0,
    Admin = 1,
    Viewer = 2
}

public sealed class Tenant
{
    public Guid TenantId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<TenantMember> Members { get; set; } = new();
}

public sealed class User
{
    public Guid UserId { get; set; } = Guid.NewGuid();
    public string ExternalId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

    public List<TenantMember> Memberships { get; set; } = new();
}

public sealed class TenantMember
{
    public Guid TenantId { get; set; }
    public Tenant Tenant { get; set; } = default!;

    public Guid UserId { get; set; }
    public User User { get; set; } = default!;

    public TenantRole Role { get; set; } = TenantRole.Viewer;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
}
