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

    [BindProperty]
    public string? AiOutput { get; set; }

    [BindProperty]
    public string? AiBlockedAction { get; set; }

    [BindProperty]
    public string? AiBlockedReason { get; set; }

    [BindProperty]
    public string? AiRequiredPlan { get; set; }

    [BindProperty]
    public string? AiRemainingQuota { get; set; }

    public new bool NotFound { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetails { get; private set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public async Task<IActionResult> OnGetAsync(string operationId)
    {
        return await LoadPageAsync(operationId);
    }

    public async Task<IActionResult> OnPostExplainAsync(string operationId)
    {
        var result = await LoadPageAsync(operationId);
        if (result is not PageResult || ProjectId is null || Operation is null)
        {
            return result;
        }

        var explainResult = await _apiTesterWebClient.ExplainOperationAsync(ProjectId.Value, Operation.OperationId, HttpContext.RequestAborted);
        if (explainResult.IsSuccess && explainResult.Payload is not null)
        {
            AiOutput = explainResult.Payload.Markdown;
            return Page();
        }

        SetGateState("Generate operation explanation", explainResult.Gate);
        return Page();
    }

    private async Task<IActionResult> LoadPageAsync(string operationId)
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

    private void SetGateState(string blockedAction, AiGateFailure? gate)
    {
        AiBlockedAction = blockedAction;
        AiBlockedReason = gate is null ? "The AI request was blocked." : $"{gate.Title}: {gate.Detail}";

        if (gate is null)
        {
            AiRequiredPlan = "Upgrade required";
            AiRemainingQuota = "Unknown";
            return;
        }

        var detail = gate.Detail;
        AiRequiredPlan = detail.Contains("Team subscription", StringComparison.OrdinalIgnoreCase)
            ? "Team"
            : detail.Contains("current plan", StringComparison.OrdinalIgnoreCase)
                ? "A plan with AI access"
                : detail.Contains("allows", StringComparison.OrdinalIgnoreCase)
                    ? "A higher quota plan"
                    : "Check plan requirements";

        AiRemainingQuota = gate.Title.Contains("quota exceeded", StringComparison.OrdinalIgnoreCase)
            ? "0"
            : "Not provided";
    }
}
