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
            RedirectToSignIn(context);
            return;
        }

        context.Items[ApiKeyAuthDefaults.OwnerKeyItemName] = apiKey;

        if (!_onboardingStore.IsComplete() && !IsOnboardingPath(context))
        {
            context.Response.Redirect("/Onboarding");
            return;
        }

        await next(context);
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
