using Microsoft.Playwright;

namespace ApiTester.Ui.E2E;

public class UiE2eTests : IClassFixture<E2eFixture>, IAsyncLifetime
{
    private readonly E2eFixture _fixture;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public UiE2eTests(E2eFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
        {
            Headless = true
        });
    }

    public async Task DisposeAsync()
    {
        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task SignIn_ShowsError_WhenKeyInvalid()
    {
        await using var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _fixture.UiBaseUri.ToString()
        });
        var page = await context.NewPageAsync();

        await page.GotoAsync("/Auth/SignIn");
        await page.GetByTestId("api-key-input").FillAsync("invalid-key");
        await page.GetByTestId("sign-in-submit").ClickAsync();

        var error = page.GetByTestId("sign-in-error");
        await error.WaitForAsync();

        Assert.Contains("not authorized", await error.InnerTextAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignIn_CompletesOnboarding_AndDisplaysProjects()
    {
        await using var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _fixture.UiBaseUri.ToString()
        });
        var page = await context.NewPageAsync();

        await SignInAndCompleteOnboardingAsync(page);

        await page.GotoAsync("/");
        await page.GetByTestId("projects-table").WaitForAsync();

        var projectRows = page.GetByTestId("project-row");
        await projectRows.First.WaitForAsync();
        var tableContent = await page.GetByTestId("projects-table").InnerTextAsync();

        Assert.Contains(_fixture.ProjectKey, tableContent, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SignOut_RequiresReauthentication()
    {
        await using var context = await _browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _fixture.UiBaseUri.ToString()
        });
        var page = await context.NewPageAsync();

        await SignInAndCompleteOnboardingAsync(page);

        await page.GotoAsync("/");
        await page.GetByTestId("sign-out-button").ClickAsync();
        await page.WaitForURLAsync("**/Auth/SignIn");

        await page.GotoAsync("/Runs");
        await page.WaitForURLAsync("**/Auth/SignIn**");
        Assert.Contains("/Auth/SignIn", page.Url, StringComparison.OrdinalIgnoreCase);
    }

    private async Task SignInAndCompleteOnboardingAsync(IPage page)
    {
        await page.GotoAsync("/Auth/SignIn");
        await page.GetByTestId("api-key-input").FillAsync(_fixture.ApiKey);
        await page.GetByTestId("sign-in-submit").ClickAsync();

        await page.WaitForURLAsync("**/Onboarding");
        await page.GotoAsync("/Onboarding/Checklist");

        await page.Locator("input[value='project']").CheckAsync();
        await page.Locator("input[value='spec']").CheckAsync();
        await page.Locator("input[value='run']").CheckAsync();
        await page.Locator("input[value='alerts']").CheckAsync();

        await page.GetByRole(AriaRole.Button, new() { Name = "Save checklist" }).ClickAsync();
        await page.GetByText("Checklist complete").WaitForAsync();
    }
}
