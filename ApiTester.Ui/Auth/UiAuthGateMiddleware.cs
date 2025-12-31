using Microsoft.AspNetCore.Http.Extensions;

namespace ApiTester.Ui.Auth;

public sealed class UiAuthGateMiddleware : IMiddleware
{
    private readonly ApiKeySessionStore _sessionStore;
    private readonly Onboarding.OnboardingSessionStore _onboardingStore;

    public UiAuthGateMiddleware(
        ApiKeySessionStore sessionStore,
        Onboarding.OnboardingSessionStore onboardingStore)
    {
        _sessionStore = sessionStore;
        _onboardingStore = onboardingStore;
    }

    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (IsAnonymousPath(context))
        {
            await next(context);
            return;
        }

        if (!_sessionStore.TryGetApiKey(out var apiKey))
        {
            if (!TryAuthenticateFromHeader(context, out apiKey))
            {
                RedirectToSignIn(context);
                return;
            }
        }

        context.Items[ApiKeyAuthDefaults.OwnerKeyItemName] = apiKey;

        if (!_onboardingStore.IsComplete() && !IsOnboardingPath(context))
        {
            context.Response.Redirect("/Onboarding");
            return;
        }

        await next(context);
    }

    private bool TryAuthenticateFromHeader(HttpContext context, out string apiKey)
    {
        apiKey = string.Empty;

        if (!context.Request.Headers.TryGetValue(ApiKeyAuthDefaults.HeaderName, out var headerKey))
        {
            return false;
        }

        if (!_sessionStore.TrySignIn(headerKey.ToString(), out _))
        {
            return false;
        }

        if (!_sessionStore.TryGetApiKey(out apiKey))
        {
            return false;
        }

        _onboardingStore.MarkComplete();
        return true;
    }

    private static bool IsAnonymousPath(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/auth", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (context.Request.Path.StartsWithSegments("/styles", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (context.Request.Path.StartsWithSegments("/images", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (context.Request.Path.StartsWithSegments("/ping", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(context.Request.Path.Value, "/favicon.ico", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsOnboardingPath(HttpContext context)
    {
        return context.Request.Path.StartsWithSegments("/onboarding", StringComparison.OrdinalIgnoreCase);
    }

    private static void RedirectToSignIn(HttpContext context)
    {
        var returnUrl = context.Request.GetEncodedPathAndQuery();
        var targetUrl = string.IsNullOrWhiteSpace(returnUrl)
            ? "/Auth/SignIn"
            : $"/Auth/SignIn?returnUrl={Uri.EscapeDataString(returnUrl)}";

        context.Response.Redirect(targetUrl);
    }
}
