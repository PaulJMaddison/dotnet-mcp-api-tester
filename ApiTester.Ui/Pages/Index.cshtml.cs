using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ApiTester.Ui.Pages;

public class IndexModel : PageModel
{
    private readonly ApiTesterWebClient _apiTesterWebClient;

    public IndexModel(ApiTesterWebClient apiTesterWebClient)
    {
        _apiTesterWebClient = apiTesterWebClient;
    }

    public IReadOnlyList<ProjectDto> Projects { get; private set; } = Array.Empty<ProjectDto>();

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetails { get; private set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task OnGetAsync()
    {
        try
        {
            var response = await _apiTesterWebClient.ListProjects(50, HttpContext.RequestAborted);
            Projects = response.Projects;
        }
        catch (Exception ex)
        {
            ErrorMessage = "We couldn't load projects right now. Please try again.";
            ErrorDetails = $"{ex.GetType().Name}: {ex.Message}";
        }
    }
}
