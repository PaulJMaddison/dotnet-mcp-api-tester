namespace ApiTester.Web.Auth;

public interface ITenantContext
{
    Guid TenantId { get; }
}

public sealed record TenantContext(Guid TenantId) : ITenantContext;
