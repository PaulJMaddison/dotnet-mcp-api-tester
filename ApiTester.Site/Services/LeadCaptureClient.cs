using System.Net.Http.Json;
using ApiTester.Site.Models;
using Microsoft.AspNetCore.Components;

namespace ApiTester.Site.Services;

public sealed class LeadCaptureClient
{
    private readonly HttpClient _httpClient;

    public LeadCaptureClient(HttpClient httpClient, NavigationManager navigationManager)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress ??= new Uri(navigationManager.BaseUri);
    }

    public async Task<LeadCaptureClientResult> SubmitAsync(LeadCaptureRequest request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.PostAsJsonAsync("/api/leads", request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return LeadCaptureClientResult.Accepted();
        }

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<LeadCaptureErrorResponse>(cancellationToken: cancellationToken);
            if (errorResponse is not null)
            {
                return new LeadCaptureClientResult(false, errorResponse.Errors);
            }
        }

        return new LeadCaptureClientResult(false, new[] { "We could not submit your request. Please try again." });
    }
}

public sealed record LeadCaptureClientResult(bool IsAccepted, IReadOnlyList<string> Errors)
{
    public static LeadCaptureClientResult Accepted() => new(true, Array.Empty<string>());
}
