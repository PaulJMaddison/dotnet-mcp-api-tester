using ApiTester.Ui.Onboarding;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ApiTester.Ui.Pages.Onboarding;

public class ChecklistModel : PageModel
{
    private readonly OnboardingSessionStore _onboardingStore;

    private static readonly string[] ChecklistKeys = ["project", "spec", "run", "alerts"];

    public ChecklistModel(OnboardingSessionStore onboardingStore)
    {
        _onboardingStore = onboardingStore;
    }

    [BindProperty]
    public string[] ChecklistItems { get; set; } = [];

    public string? Message { get; private set; }

    public void OnGet()
    {
        ChecklistItems = [];
    }

    public IActionResult OnPost()
    {
        var selected = ChecklistItems ?? [];
        var completed = ChecklistKeys.All(key => selected.Contains(key, StringComparer.OrdinalIgnoreCase));

        Message = completed
            ? "Checklist complete. You can now explore projects and runs."
            : "Checklist saved. Complete all items to finish onboarding.";

        if (completed)
        {
            _onboardingStore.MarkComplete();
        }

        return Page();
    }

    public bool IsChecked(string key)
    {
        return ChecklistItems.Contains(key, StringComparer.OrdinalIgnoreCase);
    }
}
