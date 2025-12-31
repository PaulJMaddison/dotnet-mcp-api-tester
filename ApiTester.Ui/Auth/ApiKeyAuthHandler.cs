using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;

namespace ApiTester.Ui.Auth;

public sealed class ApiKeyAuthHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ApiKeyAuthHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var context = _httpContextAccessor.HttpContext;
        var apiKey = context?.Session.GetString(ApiKeySessionStore.SessionKey);
        if (!string.IsNullOrWhiteSpace(apiKey) && !request.Headers.Contains(ApiKeyAuthDefaults.HeaderName))
        {
            request.Headers.Add(ApiKeyAuthDefaults.HeaderName, apiKey);
        }

        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return base.SendAsync(request, cancellationToken);
    }
}
