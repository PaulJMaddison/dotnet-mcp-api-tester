using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ApiTester.Ui.Pages.Runs;

public class IndexModel : PageModel
{
    private readonly ApiTesterWebClient _apiTesterWebClient;

    public IndexModel(ApiTesterWebClient apiTesterWebClient)
    {
        _apiTesterWebClient = apiTesterWebClient;
    }

    public RunDetailDto? Run { get; private set; }

    public bool NotFound { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetails { get; private set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task OnGetAsync(Guid runId)
    {
        try
        {
            var run = await _apiTesterWebClient.GetRun(runId, HttpContext.RequestAborted);
            if (run is null)
            {
                NotFound = true;
                return;
            }

            Run = run;
        }
        catch (Exception ex)
        {
            ErrorMessage = "We couldn't load this run right now. Please try again.";
            ErrorDetails = $"{ex.GetType().Name}: {ex.Message}";
        }
    }
}
