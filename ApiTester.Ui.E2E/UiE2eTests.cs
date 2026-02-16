using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Playwright;

namespace ApiTester.Ui.E2E;

public class UiE2eTests : IClassFixture<E2eFixture>, IAsyncLifetime
{
    private readonly E2eFixture _fixture;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;
    private bool _skipBrowserTests;
    private static bool _browsersReady;
    private static readonly SemaphoreSlim BrowserInstallLock = new(1, 1);

    public UiE2eTests(E2eFixture fixture)
    {
        _fixture = fixture;
    }

    public async Task InitializeAsync()
    {
        _browsersReady = await EnsurePlaywrightBrowsersAsync();
        if (!_browsersReady)
        {
            _skipBrowserTests = true;
            return;
        }

        try
        {
            _playwright = await Playwright.CreateAsync();
            var launchOptions = new BrowserTypeLaunchOptions
            {
                Headless = true
            };

            if (!File.Exists(_playwright.Chromium.ExecutablePath))
            {
                var systemChromium = ResolveSystemChromiumPath();
                if (!string.IsNullOrWhiteSpace(systemChromium))
                {
                    launchOptions.ExecutablePath = systemChromium;
                }
            }

            _browser = await _playwright.Chromium.LaunchAsync(launchOptions);
        }
        catch (PlaywrightException)
        {
            _skipBrowserTests = true;
            _playwright?.Dispose();
        }
    }

    public async Task DisposeAsync()
    {
        if (_skipBrowserTests)
            return;

        await _browser.CloseAsync();
        _playwright.Dispose();
    }

    [Fact]
    public async Task SignIn_ShowsError_WhenKeyInvalid()
    {
        if (ShouldSkipBrowserTests())
            return;

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
        if (ShouldSkipBrowserTests())
            return;

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
        if (ShouldSkipBrowserTests())
            return;

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

    private static async Task<bool> EnsurePlaywrightBrowsersAsync()
    {
        if (_browsersReady)
            return true;

        await BrowserInstallLock.WaitAsync();
        try
        {
            if (_browsersReady)
                return true;

            if (!string.IsNullOrWhiteSpace(ResolveSystemChromiumPath()))
            {
                _browsersReady = true;
                return true;
            }

            var cacheDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache",
                "ms-playwright");
            var hasChromium = HasChromium(cacheDir);

            if (!hasChromium)
            {
                try
                {
                    Microsoft.Playwright.Program.Main(new[] { "install", "chromium" });
                }
                catch
                {
                }

                hasChromium = HasChromium(cacheDir);
            }

            _browsersReady = hasChromium;
            return _browsersReady;
        }
        finally
        {
            BrowserInstallLock.Release();
        }
    }

    private bool ShouldSkipBrowserTests() => _skipBrowserTests || !_fixture.IsReady;

    private static bool HasChromium(string cacheDir)
    {
        if (!string.IsNullOrWhiteSpace(ResolveSystemChromiumPath()))
            return true;

        return Directory.Exists(cacheDir)
            && Directory.EnumerateFiles(cacheDir, "chrome", SearchOption.AllDirectories).Any();
    }

    private static string? ResolveSystemChromiumPath()
    {
        var candidates = new[]
        {
            "/usr/bin/chromium",
            "/usr/bin/chromium-browser",
            "/usr/bin/google-chrome",
            "/usr/bin/google-chrome-stable"
        };

        return candidates.FirstOrDefault(File.Exists);
    }
}
