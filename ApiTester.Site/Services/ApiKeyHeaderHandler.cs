namespace ApiTester.Site.Services;

public sealed class ApiKeyHeaderHandler : DelegatingHandler
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly IApiKeySession _apiKeySession;

    public ApiKeyHeaderHandler(IApiKeySession apiKeySession)
    {
        _apiKeySession = apiKeySession;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var apiKey = _apiKeySession.GetApiKey();
        if (!string.IsNullOrWhiteSpace(apiKey) && !request.Headers.Contains(ApiKeyHeaderName))
        {
            request.Headers.Add(ApiKeyHeaderName, apiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
