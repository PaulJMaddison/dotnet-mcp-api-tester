using ApiTester.Ui.Auth;
using ApiTester.Ui.Onboarding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ApiTester.Ui.Pages.Auth;

public class SignInModel : PageModel
{
    private readonly ApiKeySessionStore _sessionStore;
    private readonly OnboardingSessionStore _onboardingStore;

    public SignInModel(ApiKeySessionStore sessionStore, OnboardingSessionStore onboardingStore)
    {
        _sessionStore = sessionStore;
        _onboardingStore = onboardingStore;
    }

    [BindProperty]
    public string? ApiKey { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public IActionResult OnGet()
    {
        if (_sessionStore.TryGetApiKey(out _))
        {
            return Redirect("/Projects");
        }

        return Page();
    }

    public IActionResult OnPost()
    {
        if (!_sessionStore.TrySignIn(ApiKey, out var errorMessage))
        {
            ErrorMessage = errorMessage;
            return Page();
        }

        if (!_onboardingStore.IsComplete())
        {
            return Redirect("/Onboarding");
        }

        if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
        {
            return Redirect(ReturnUrl);
        }

        return Redirect("/Projects");
    }
}
