using System.Net;
using System.Text;
using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc;
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

    public bool IsLoading { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid runId)
    {
        return await LoadPageAsync(runId);
    }

    public async Task<IActionResult> OnPostSummariseAsync(Guid runId)
    {
        var result = await LoadPageAsync(runId);
        if (result is not PageResult || Run is null)
        {
            return result;
        }

        var summaryResult = await _apiTesterWebClient.SummariseRunAsync(runId, HttpContext.RequestAborted);
        if (summaryResult.IsSuccess && summaryResult.Payload is not null)
        {
            AiOutput = BuildRunSummaryMarkdown(summaryResult.Payload);
            return Page();
        }

        SetGateState("Generate run summary", summaryResult.Gate);
        return Page();
    }

    private async Task<IActionResult> LoadPageAsync(Guid runId)
    {
        try
        {
            IsLoading = true;
            var run = await _apiTesterWebClient.GetRun(runId, HttpContext.RequestAborted);
            if (run is null)
            {
                NotFound = true;
                return Page();
            }

            Run = run;
            return Page();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Redirect($"/Auth/SignIn?returnUrl={Uri.EscapeDataString(HttpContext.Request.Path + HttpContext.Request.QueryString)}");
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

        return Page();
    }

    private static string BuildRunSummaryMarkdown(AiRunSummaryResponse response)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# AI Run Summary");
        sb.AppendLine();
        sb.AppendLine(response.OverallSummary);
        sb.AppendLine();
        sb.AppendLine($"Flake assessment: {response.FlakeAssessment}");
        sb.AppendLine();
        sb.AppendLine($"Regression likelihood: {response.RegressionLikelihood.Level} — {response.RegressionLikelihood.Rationale}");

        if (response.TopFailures.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Top failures:");
            foreach (var failure in response.TopFailures)
            {
                sb.AppendLine($"- {failure.Title}");
                foreach (var evidence in failure.EvidenceRefs)
                {
                    sb.AppendLine($"  - {evidence.CaseName}: {evidence.FailureReason ?? "No failure reason provided."}");
                }
            }
        }

        if (response.RecommendedNextActions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Recommended next actions:");
            foreach (var action in response.RecommendedNextActions)
            {
                sb.AppendLine($"- {action}");
            }
        }

        return sb.ToString().Trim();
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
