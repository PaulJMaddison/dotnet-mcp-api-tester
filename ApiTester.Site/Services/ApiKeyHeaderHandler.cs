using Microsoft.Extensions.Options;

namespace ApiTester.Site.Services;

public sealed class ApiKeyHeaderHandler : DelegatingHandler
{
    private const string ApiKeyHeaderName = "X-Api-Key";
    private readonly IOptions<ApiTesterWebOptions> _options;

    public ApiKeyHeaderHandler(IOptions<ApiTesterWebOptions> options)
    {
        _options = options;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var apiKey = _options.Value.ApiKey;
        if (!string.IsNullOrWhiteSpace(apiKey) && !request.Headers.Contains(ApiKeyHeaderName))
        {
            request.Headers.Add(ApiKeyHeaderName, apiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
