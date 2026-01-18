using Microsoft.AspNetCore.Http;

namespace ApiTester.Web.Auth;

public static class TenantContextExtensions
{
    public static ITenantContext GetTenantContext(this HttpContext context)
    {
        if (context.Items.TryGetValue(TenantContextMiddleware.ContextItemName, out var value)
            && value is ITenantContext tenantContext)
        {
            return tenantContext;
        }

        throw new InvalidOperationException("Tenant context not available on the current request.");
    }
}
