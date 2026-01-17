using Microsoft.AspNetCore.Http;

namespace ApiTester.Site.Services;

public interface IApiKeySession
{
    bool HasApiKey { get; }
    string? GetApiKey();
    void SetApiKey(string apiKey);
    void ClearApiKey();
}

public sealed class ApiKeySession : IApiKeySession
{
    private const string SessionKey = "ApiTester.ApiKey";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApiKeySession(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool HasApiKey => !string.IsNullOrWhiteSpace(GetApiKey());

    public string? GetApiKey()
        => _httpContextAccessor.HttpContext?.Session.GetString(SessionKey);

    public void SetApiKey(string apiKey)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            context.Session.Remove(SessionKey);
            return;
        }

        context.Session.SetString(SessionKey, apiKey.Trim());
    }

    public void ClearApiKey()
        => _httpContextAccessor.HttpContext?.Session.Remove(SessionKey);
}
