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

    public string? ProjectKey { get; private set; }

    public string? OperationId { get; private set; }

    public int Take { get; private set; } = 20;

    public IReadOnlyList<RunSummaryDto> Runs { get; private set; } = Array.Empty<RunSummaryDto>();

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetails { get; private set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsLoading { get; private set; }

    public bool HasFilters => !string.IsNullOrWhiteSpace(ProjectKey) || !string.IsNullOrWhiteSpace(OperationId);

    public async Task OnGetAsync(string? projectKey, string? operationId, int? take)
    {
        ProjectKey = string.IsNullOrWhiteSpace(projectKey) ? null : projectKey.Trim();
        OperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
        Take = NormalizeTake(take);

        if (string.IsNullOrWhiteSpace(ProjectKey))
        {
            return;
        }

        try
        {
            IsLoading = true;
            var response = await _apiTesterWebClient.ListRuns(ProjectKey, Take, OperationId, HttpContext.RequestAborted);
            Runs = response.Runs;
        }
        catch (Exception ex)
        {
            ErrorMessage = "We couldn't load runs right now. Please try again.";
            ErrorDetails = $"{ex.GetType().Name}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static int NormalizeTake(int? take)
        => take switch
        {
            10 => 10,
            50 => 50,
            _ => 20
        };
}
