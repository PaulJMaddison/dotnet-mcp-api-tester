using System.Net;
using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ApiTester.Ui.Pages.Specs;

public class DetailsModel : PageModel
{
    private readonly ApiTesterWebClient _apiTesterWebClient;

    public DetailsModel(ApiTesterWebClient apiTesterWebClient)
    {
        _apiTesterWebClient = apiTesterWebClient;
    }

    public OpenApiSpecDetailDto? Spec { get; private set; }

    public IReadOnlyList<OpenApiOperationSummaryDto> Operations { get; private set; } = Array.Empty<OpenApiOperationSummaryDto>();

    public Guid? ProjectId { get; private set; }

    public new bool NotFound { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetails { get; private set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task<IActionResult> OnGetAsync(Guid id, Guid? projectId)
    {
        try
        {
            Spec = await _apiTesterWebClient.GetOpenApiSpecDetail(id, HttpContext.RequestAborted);
            if (Spec is null)
            {
                NotFound = true;
                return Page();
            }

            ProjectId = projectId ?? Spec.ProjectId;

            var response = await _apiTesterWebClient.ListOperations(ProjectId.Value, HttpContext.RequestAborted);
            Operations = response.Operations;
            return Page();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var returnUrl = Uri.EscapeDataString(HttpContext.Request.Path + HttpContext.Request.QueryString);
            return Redirect($"/Auth/SignIn?returnUrl={returnUrl}");
        }
        catch (Exception ex)
        {
            ErrorMessage = "We couldn't load operations for this spec right now. Please try again.";
            ErrorDetails = $"{ex.GetType().Name}: {ex.Message}";
            return Page();
        }
    }
}
