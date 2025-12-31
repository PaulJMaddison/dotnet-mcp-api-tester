using System.Net;
using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ApiTester.Ui.Pages.Projects;

public class DetailsModel : PageModel
{
    private readonly ApiTesterWebClient _apiTesterWebClient;

    public DetailsModel(ApiTesterWebClient apiTesterWebClient)
    {
        _apiTesterWebClient = apiTesterWebClient;
    }

    public ProjectDto? Project { get; private set; }

    public IReadOnlyList<RunSummaryDto> Runs { get; private set; } = Array.Empty<RunSummaryDto>();

    public string? OperationId { get; private set; }

    public int Take { get; private set; } = 20;

    public bool NotFound { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetails { get; private set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsLoading { get; private set; }

    public async Task OnGetAsync(Guid projectId, string? operationId, int? take)
    {
        OperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
        Take = NormalizeTake(take);

        try
        {
            IsLoading = true;
            Project = await _apiTesterWebClient.GetProject(projectId, HttpContext.RequestAborted);
            var response = await _apiTesterWebClient.ListRuns(Project.ProjectKey, Take, OperationId, HttpContext.RequestAborted);
            Runs = response.Runs;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            NotFound = true;
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
