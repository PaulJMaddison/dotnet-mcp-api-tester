using ApiTester.Ui.Auth;
using ApiTester.Ui.Onboarding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ApiTester.Ui.Pages.Auth;

public class SignOutModel : PageModel
{
    private readonly ApiKeySessionStore _sessionStore;
    private readonly OnboardingSessionStore _onboardingStore;

    public SignOutModel(ApiKeySessionStore sessionStore, OnboardingSessionStore onboardingStore)
    {
        _sessionStore = sessionStore;
        _onboardingStore = onboardingStore;
    }

    public IActionResult OnPost()
    {
        _sessionStore.SignOut();
        _onboardingStore.Reset();
        return Redirect("/Auth/SignIn");
    }

    public void OnGet()
    {
    }
}
