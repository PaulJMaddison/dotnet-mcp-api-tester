using System.Net;
using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ApiTester.Ui.Pages.Operations;

public class DetailsModel : PageModel
{
    private readonly ApiTesterWebClient _apiTesterWebClient;

    public DetailsModel(ApiTesterWebClient apiTesterWebClient)
    {
        _apiTesterWebClient = apiTesterWebClient;
    }

    public OpenApiOperationDescribeResponse? Operation { get; private set; }

    [BindProperty(SupportsGet = true)]
    public Guid? ProjectId { get; set; }

    public new bool NotFound { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetails { get; private set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task<IActionResult> OnGetAsync(string operationId)
    {
        if (ProjectId is null)
        {
            ErrorMessage = "A projectId query value is required to describe an operation.";
            return Page();
        }

        try
        {
            Operation = await _apiTesterWebClient.DescribeOperation(ProjectId.Value, operationId, HttpContext.RequestAborted);
            if (Operation is null)
            {
                NotFound = true;
            }

            return Page();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            var returnUrl = Uri.EscapeDataString(HttpContext.Request.Path + HttpContext.Request.QueryString);
            return Redirect($"/Auth/SignIn?returnUrl={returnUrl}");
        }
        catch (Exception ex)
        {
            ErrorMessage = "We couldn't describe that operation right now. Please try again.";
            ErrorDetails = $"{ex.GetType().Name}: {ex.Message}";
            return Page();
        }
    }
}
