using Microsoft.AspNetCore.Http;

namespace ApiTester.Ui.Onboarding;

public sealed class OnboardingSessionStore
{
    public const string SessionKey = "Onboarding.Complete";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public OnboardingSessionStore(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public bool IsComplete()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null)
        {
            return false;
        }

        return string.Equals(context.Session.GetString(SessionKey), "true", StringComparison.OrdinalIgnoreCase);
    }

    public void MarkComplete()
    {
        var context = _httpContextAccessor.HttpContext;
        context?.Session.SetString(SessionKey, "true");
    }

    public void Reset()
    {
        var context = _httpContextAccessor.HttpContext;
        context?.Session.Remove(SessionKey);
    }
}
