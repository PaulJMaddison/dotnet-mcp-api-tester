using Microsoft.AspNetCore.Http;

namespace ApiTester.Web.Auth;

public static class ApiKeyAuthExtensions
{
    public static string GetOwnerKey(this HttpContext context)
    {
        if (context.Items.TryGetValue(ApiKeyAuthDefaults.OwnerKeyItemName, out var value) && value is string ownerKey)
            return ownerKey;

        throw new InvalidOperationException("Owner key not available on the current request.");
    }
}
