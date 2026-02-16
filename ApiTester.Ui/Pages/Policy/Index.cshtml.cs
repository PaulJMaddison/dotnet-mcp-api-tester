using System.Net;
using System.Text.Json;
using ApiTester.Ui.Clients;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ApiTester.Ui.Pages.Policy;

public class IndexModel : PageModel
{
    private readonly ApiTesterWebClient _apiTesterWebClient;

    public IndexModel(ApiTesterWebClient apiTesterWebClient)
    {
        _apiTesterWebClient = apiTesterWebClient;
    }

    [BindProperty]
    public string AllowedBaseUrlsJson { get; set; } = "[]";

    [BindProperty]
    public string AllowedMethodsJson { get; set; } = "[]";

    [BindProperty]
    public string TimeoutSecondsInput { get; set; } = "";

    [BindProperty]
    public string MaxRequestBodyBytesInput { get; set; } = "";

    [BindProperty]
    public string MaxResponseBodyBytesInput { get; set; } = "";

    public List<string> ValidationErrors { get; } = [];

    public string? SaveMessage { get; private set; }

    public string? ErrorMessage { get; private set; }

    public string? ErrorDetails { get; private set; }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool IsLoading { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        try
        {
            IsLoading = true;
            var policy = await _apiTesterWebClient.GetPolicy(HttpContext.RequestAborted);
            BindFromPolicy(policy);
            return Page();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Redirect($"/Auth/SignIn?returnUrl={Uri.EscapeDataString(HttpContext.Request.Path + HttpContext.Request.QueryString)}");
        }
        catch (Exception ex)
        {
            ErrorMessage = "We couldn't load policy settings right now. Please try again.";
            ErrorDetails = $"{ex.GetType().Name}: {ex.Message}";
            return Page();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var allowedBaseUrls = TryParseStringArray(AllowedBaseUrlsJson, "Allowlists (JSON array)");
        var allowedMethods = TryParseStringArray(AllowedMethodsJson, "Methods (JSON array)");
        var timeoutSeconds = TryParsePositiveInt(TimeoutSecondsInput, "Timeout (seconds)");
        var maxRequestBodyBytes = TryParsePositiveInt(MaxRequestBodyBytesInput, "Max request body bytes");
        var maxResponseBodyBytes = TryParsePositiveInt(MaxResponseBodyBytesInput, "Max response body bytes");

        if (ValidationErrors.Count > 0 ||
            allowedBaseUrls is null ||
            allowedMethods is null ||
            timeoutSeconds is null ||
            maxRequestBodyBytes is null ||
            maxResponseBodyBytes is null)
        {
            return Page();
        }

        try
        {
            var updateRequest = new ApiPolicyUpdateRequest(
                DryRun: null,
                AllowedBaseUrls: allowedBaseUrls,
                AllowedMethods: allowedMethods,
                TimeoutSeconds: timeoutSeconds,
                MaxRequestBodyBytes: maxRequestBodyBytes,
                MaxResponseBodyBytes: maxResponseBodyBytes,
                ValidateSchema: null,
                BlockLocalhost: null,
                BlockPrivateNetworks: null,
                RetryOnFlake: null,
                MaxRetries: null);

            var policy = await _apiTesterWebClient.UpdatePolicy(updateRequest, HttpContext.RequestAborted);
            BindFromPolicy(policy);
            SaveMessage = "Policy saved.";
            return Page();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            return Redirect($"/Auth/SignIn?returnUrl={Uri.EscapeDataString(HttpContext.Request.Path + HttpContext.Request.QueryString)}");
        }
        catch (Exception ex)
        {
            ErrorMessage = "We couldn't save policy settings right now. Please try again.";
            ErrorDetails = $"{ex.GetType().Name}: {ex.Message}";
            return Page();
        }
    }

    private void BindFromPolicy(ApiPolicyResponse policy)
    {
        AllowedBaseUrlsJson = JsonSerializer.Serialize(policy.AllowedBaseUrls);
        AllowedMethodsJson = JsonSerializer.Serialize(policy.AllowedMethods);
        TimeoutSecondsInput = policy.TimeoutSeconds.ToString();
        MaxRequestBodyBytesInput = policy.MaxRequestBodyBytes.ToString();
        MaxResponseBodyBytesInput = policy.MaxResponseBodyBytes.ToString();
    }

    private IReadOnlyList<string>? TryParseStringArray(string input, string label)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            ValidationErrors.Add($"{label} is required.");
            return null;
        }

        try
        {
            using var json = JsonDocument.Parse(input);
            if (json.RootElement.ValueKind != JsonValueKind.Array)
            {
                ValidationErrors.Add($"{label} must be a JSON array of strings.");
                return null;
            }

            var values = new List<string>();
            foreach (var element in json.RootElement.EnumerateArray())
            {
                if (element.ValueKind != JsonValueKind.String)
                {
                    ValidationErrors.Add($"{label} must contain only strings.");
                    return null;
                }

                var value = element.GetString();
                if (string.IsNullOrWhiteSpace(value))
                {
                    ValidationErrors.Add($"{label} cannot contain empty strings.");
                    return null;
                }

                values.Add(value.Trim());
            }

            return values;
        }
        catch (JsonException ex)
        {
            ValidationErrors.Add($"{label} is invalid JSON: {ex.Message}");
            return null;
        }
    }

    private int? TryParsePositiveInt(string input, string label)
    {
        if (!int.TryParse(input, out var value) || value <= 0)
        {
            ValidationErrors.Add($"{label} must be a positive integer.");
            return null;
        }

        return value;
    }
}
