using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ApiTester.Ui.Pages.Runs;

public class DetailsModel : PageModel
{
    private readonly ApiTesterWebClient _apiTesterWebClient;

    public DetailsModel(ApiTesterWebClient apiTesterWebClient)
    {
        _apiTesterWebClient = apiTesterWebClient;
    }

    public RunDetailDto? Run { get; private set; }

    public new bool NotFound { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetails { get; private set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsLoading { get; private set; }

    public async Task OnGetAsync(Guid runId)
    {
        try
        {
            IsLoading = true;
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
        finally
        {
            IsLoading = false;
        }
    }
}
