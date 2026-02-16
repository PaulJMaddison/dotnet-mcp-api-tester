using System.Net;
using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ApiTester.Ui.Pages.Specs;

public class IndexModel : PageModel
{
    private readonly ApiTesterWebClient _apiTesterWebClient;

    public IndexModel(ApiTesterWebClient apiTesterWebClient)
    {
        _apiTesterWebClient = apiTesterWebClient;
    }

    [BindProperty(SupportsGet = true)]
    public Guid? ProjectId { get; set; }

    public IReadOnlyList<ProjectDto> Projects { get; private set; } = Array.Empty<ProjectDto>();

    public IReadOnlyList<OpenApiSpecMetadataDto> Specs { get; private set; } = Array.Empty<OpenApiSpecMetadataDto>();

    [BindProperty]
    public IFormFile? UploadFile { get; set; }

    [BindProperty]
    public string? ImportUrl { get; set; }

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetails { get; private set; }

    public string? ImportResultMessage { get; private set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            await LoadAsync();
            return Page();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return RedirectToSignIn();
        }
    }

    public async Task<IActionResult> OnPostImportAsync()
    {
        if (ProjectId is null)
        {
            ErrorMessage = "Select a project before importing a spec.";
            await LoadProjectsOnlyAsync();
            return Page();
        }

        if (UploadFile is null && string.IsNullOrWhiteSpace(ImportUrl))
        {
            ErrorMessage = "Provide either a local file or URL to import.";
            await LoadAsync();
            return Page();
        }

        try
        {
            Stream? fileStream = null;
            var fileName = string.Empty;

            if (UploadFile is not null)
            {
                fileStream = UploadFile.OpenReadStream();
                fileName = Path.GetFileName(UploadFile.FileName);
            }

            try
            {
                var spec = await _apiTesterWebClient.ImportOpenApiSpec(
                    ProjectId.Value,
                    fileStream,
                    fileName,
                    NormalizeUntrustedInput(ImportUrl),
                    HttpContext.RequestAborted);

                ImportResultMessage = $"Imported {spec.Title} ({spec.Version}) successfully.";
            }
            finally
            {
                fileStream?.Dispose();
            }

            await LoadAsync();
            return Page();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return RedirectToSignIn();
        }
        catch (Exception ex)
        {
            ErrorMessage = "We couldn't import that spec right now. Please try again.";
            ErrorDetails = $"{ex.GetType().Name}: {ex.Message}";
            await LoadAsync();
            return Page();
        }
    }

    private async Task LoadAsync()
    {
        await LoadProjectsOnlyAsync();

        if (ProjectId is null)
        {
            ProjectId = Projects.FirstOrDefault()?.ProjectId;
        }

        if (ProjectId is not null)
        {
            Specs = await _apiTesterWebClient.ListOpenApiSpecs(ProjectId.Value, HttpContext.RequestAborted);
        }
    }

    private async Task LoadProjectsOnlyAsync()
    {
        var projectResponse = await _apiTesterWebClient.ListProjects(100, HttpContext.RequestAborted);
        Projects = projectResponse.Projects;
    }

    private string? NormalizeUntrustedInput(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private RedirectResult RedirectToSignIn()
    {
        var returnUrl = Uri.EscapeDataString(HttpContext.Request.Path + HttpContext.Request.QueryString);
        return Redirect($"/Auth/SignIn?returnUrl={returnUrl}");
    }
}
