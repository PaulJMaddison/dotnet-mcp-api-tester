using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ApiTester.Ui.Pages.Projects;

public class IndexModel : PageModel
{
    private readonly ApiTesterWebClient _apiTesterWebClient;

    public IndexModel(ApiTesterWebClient apiTesterWebClient)
    {
        _apiTesterWebClient = apiTesterWebClient;
    }

    public Guid ProjectId { get; private set; }

    public string? ProjectName { get; private set; }

    public string ProjectKey { get; private set; } = string.Empty;

    public IReadOnlyList<RunSummaryDto> Runs { get; private set; } = Array.Empty<RunSummaryDto>();

    public string? OperationId { get; private set; }

    public int Take { get; private set; } = 20;

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetails { get; private set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public OpenApiSpecMetadataDto? OpenApiSpec { get; private set; }

    [BindProperty]
    public IFormFile? OpenApiFile { get; set; }

    [BindProperty]
    public string? OpenApiPath { get; set; }

    [TempData]
    public string? ImportMessage { get; set; }

    [TempData]
    public string? ImportMessageKind { get; set; }

    public async Task OnGetAsync(Guid projectId, string? operationId, int? take)
    {
        ProjectId = projectId;
        OperationId = string.IsNullOrWhiteSpace(operationId) ? null : operationId.Trim();
        Take = NormalizeTake(take);

        try
        {
            var project = await _apiTesterWebClient.GetProject(projectId, HttpContext.RequestAborted);
            ProjectKey = project.ProjectKey;
            ProjectName = string.IsNullOrWhiteSpace(project.Name) ? null : project.Name;

            var response = await _apiTesterWebClient.ListRuns(project.ProjectKey, Take, OperationId, HttpContext.RequestAborted);
            Runs = response.Runs;

            OpenApiSpec = await _apiTesterWebClient.GetOpenApiSpec(projectId, HttpContext.RequestAborted);
        }
        catch (Exception ex)
        {
            ErrorMessage = "We couldn't load runs for this project right now. Please try again.";
            ErrorDetails = $"{ex.GetType().Name}: {ex.Message}";
        }
    }

    public async Task<IActionResult> OnPostImportAsync(Guid projectId)
    {
        if (OpenApiFile is null && string.IsNullOrWhiteSpace(OpenApiPath))
        {
            ImportMessage = "Select an OpenAPI file or provide a path to import.";
            ImportMessageKind = "error";
            return RedirectToPage(new { projectId });
        }

        try
        {
            await using var stream = OpenApiFile?.OpenReadStream();
            var result = await _apiTesterWebClient.ImportOpenApiSpec(
                projectId,
                stream,
                OpenApiFile?.FileName,
                OpenApiPath,
                HttpContext.RequestAborted);

            ImportMessage = $"Imported OpenAPI spec: {result.Title} ({result.Version}).";
            ImportMessageKind = "success";
        }
        catch (Exception ex)
        {
            ImportMessage = "We couldn't import the OpenAPI spec. Please check the file and try again.";
            ImportMessageKind = "error";
            ErrorDetails = $"{ex.GetType().Name}: {ex.Message}";
        }

        return RedirectToPage(new { projectId });
    }

    private static int NormalizeTake(int? take)
        => take switch
        {
            10 => 10,
            50 => 50,
            _ => 20
        };
}
