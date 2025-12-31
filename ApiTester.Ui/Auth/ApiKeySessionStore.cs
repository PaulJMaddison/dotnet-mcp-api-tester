using Microsoft.AspNetCore.Http;

namespace ApiTester.Ui.Auth;

public sealed class ApiKeySessionStore
{
    public const string SessionKey = "Auth.ApiKey";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApiKeyAuthSettings _settings;

    public ApiKeySessionStore(IHttpContextAccessor httpContextAccessor, ApiKeyAuthSettings settings)
    {
        _httpContextAccessor = httpContextAccessor;
        _settings = settings;
    }

    public bool TryGetApiKey(out string apiKey)
    {
        apiKey = string.Empty;
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return false;
        }

        var storedKey = context.Session.GetString(SessionKey);
        if (string.IsNullOrWhiteSpace(storedKey))
        {
            return false;
        }

        apiKey = storedKey;
        return true;
    }

    public bool TrySignIn(string? apiKey, out string errorMessage)
    {
        errorMessage = string.Empty;
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            errorMessage = "Unable to access the current session.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            errorMessage = "Enter a valid API key to continue.";
            return false;
        }

        var trimmed = apiKey.Trim();
        if (!_settings.IsAllowed(trimmed))
        {
            errorMessage = "That API key is not authorized for this workspace.";
            return false;
        }

        context.Session.SetString(SessionKey, trimmed);
        return true;
    }

    public void SignOut()
    {
        var context = _httpContextAccessor.HttpContext;
        context?.Session.Remove(SessionKey);
    }
}
