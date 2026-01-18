using Microsoft.AspNetCore.Http;

namespace ApiTester.Web.Auth;

public static class OrgContextExtensions
{
    public static OrgContext GetOrgContext(this HttpContext context)
    {
        if (context.Items.TryGetValue(OrgContextMiddleware.ContextItemName, out var value) && value is OrgContext orgContext)
            return orgContext;

        throw new InvalidOperationException("Org context is missing.");
    }
}
