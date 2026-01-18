using Microsoft.AspNetCore.Http;

namespace ApiTester.Web.Auth;

public static class ApiKeyContextExtensions
{
    public static ApiKeyContext GetApiKeyContext(this HttpContext context)
    {
        if (context.Items.TryGetValue(ApiKeyAuthDefaults.ApiKeyContextItemName, out var value) && value is ApiKeyContext apiKeyContext)
            return apiKeyContext;

        throw new InvalidOperationException("API key context is missing.");
    }

    public static bool HasScope(this HttpContext context, string scope)
    {
        var apiKeyContext = context.GetApiKeyContext();
        return apiKeyContext.Scopes.Contains(scope);
    }
}
