using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ApiTester.AI.Azure;

public sealed class AzureOpenAiTransport
{
    public static readonly ActivitySource ActivitySource = new("ApiTester.AI.Azure");

    private readonly HttpClient _httpClient;
    private readonly AzureOpenAiOptions _options;
    private readonly object _circuitLock = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _circuitOpenedUntil;

    public AzureOpenAiTransport(HttpClient httpClient, AzureOpenAiOptions options)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<AzureOpenAiTransportResponse> PostJsonAsync(string relativePath, object payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            throw new ArgumentException("A relative Azure OpenAI path is required.", nameof(relativePath));

        EnsureCircuitClosed();

        var baseUri = _options.GetApiBaseUri();
        var requestUri = new Uri(baseUri, relativePath.TrimStart('/'));
        var attempts = Math.Max(1, _options.MaxRetries + 1);
        Exception? lastError = null;

        using var activity = ActivitySource.StartActivity("azure.openai.request", ActivityKind.Client);
        activity?.SetTag("ai.system", "azure_openai");
        activity?.SetTag("server.address", baseUri.Host);
        activity?.SetTag("http.request.method", "POST");
        activity?.SetTag("http.route", relativePath.TrimStart('/'));

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            activity?.SetTag("ai.retry.attempt", attempt);

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds)));

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, requestUri)
                {
                    Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };

                ApplyAuthentication(request);

                var stopwatch = Stopwatch.StartNew();
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutCts.Token).ConfigureAwait(false);
                stopwatch.Stop();

                activity?.SetTag("http.response.status_code", (int)response.StatusCode);

                if (IsTransient(response.StatusCode) && attempt < attempts)
                {
                    await DelayForRetryAsync(response, attempt, ct).ConfigureAwait(false);
                    continue;
                }

                if (!response.IsSuccessStatusCode)
                {
                    var requestId = TryGetRequestId(response);
                    throw new AzureOpenAiRequestException(response.StatusCode, requestId);
                }

                if (response.Content.Headers.ContentLength is > 0 &&
                    response.Content.Headers.ContentLength > Math.Max(1, _options.MaxResponseBytes))
                {
                    throw new InvalidOperationException(
                        $"Azure OpenAI response exceeded the configured maximum of {_options.MaxResponseBytes} bytes.");
                }

                var bytes = await response.Content.ReadAsByteArrayAsync(timeoutCts.Token).ConfigureAwait(false);
                if (bytes.Length > Math.Max(1, _options.MaxResponseBytes))
                {
                    throw new InvalidOperationException(
                        $"Azure OpenAI response exceeded the configured maximum of {_options.MaxResponseBytes} bytes.");
                }

                var requestId = TryGetRequestId(response);
                activity?.SetTag("ai.retry.count", attempt - 1);
                activity?.SetTag("ai.response.bytes", bytes.Length);
                activity?.SetTag("ai.request_id", requestId);
                activity?.SetStatus(ActivityStatusCode.Ok);

                RecordSuccess();
                return new AzureOpenAiTransportResponse(bytes, (int)stopwatch.ElapsedMilliseconds, requestId);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested && attempt < attempts)
            {
                lastError = new TimeoutException("Azure OpenAI request timed out.");
                await Task.Delay(Backoff(attempt), ct).ConfigureAwait(false);
            }
            catch (HttpRequestException ex) when (attempt < attempts)
            {
                lastError = ex;
                await Task.Delay(Backoff(attempt), ct).ConfigureAwait(false);
            }
            catch (AzureOpenAiRequestException ex) when (IsTransient(ex.StatusCode) && attempt < attempts)
            {
                lastError = ex;
                await Task.Delay(Backoff(attempt), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        RecordFailure();
        activity?.SetTag("ai.retry.count", attempts - 1);
        activity?.SetTag("error.type", lastError?.GetType().Name ?? "Unknown");
        activity?.SetStatus(ActivityStatusCode.Error, "Azure OpenAI request failed");
        throw new InvalidOperationException("Azure OpenAI request failed.", lastError);
    }

    private void ApplyAuthentication(HttpRequestMessage request)
    {
        if (!string.IsNullOrWhiteSpace(_options.BearerToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.BearerToken.Trim());
            return;
        }

        if (!string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("api-key", _options.ApiKey.Trim());
            return;
        }

        throw new InvalidOperationException(
            "Azure OpenAI credentials are not configured. Set AZURE_OPENAI_API_KEY or AZURE_OPENAI_AUTH_TOKEN.");
    }

    private void EnsureCircuitClosed()
    {
        lock (_circuitLock)
        {
            var now = DateTimeOffset.UtcNow;
            if (_circuitOpenedUntil is not null && _circuitOpenedUntil > now)
                throw new InvalidOperationException("Azure OpenAI circuit breaker is open.");

            if (_circuitOpenedUntil is not null && _circuitOpenedUntil <= now)
                _circuitOpenedUntil = null;
        }
    }

    private void RecordSuccess()
    {
        lock (_circuitLock)
        {
            _consecutiveFailures = 0;
            _circuitOpenedUntil = null;
        }
    }

    private void RecordFailure()
    {
        lock (_circuitLock)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= Math.Max(1, _options.CircuitBreakerFailureThreshold))
            {
                _circuitOpenedUntil = DateTimeOffset.UtcNow.AddSeconds(
                    Math.Max(5, _options.CircuitBreakerBreakSeconds));
            }
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode) =>
        statusCode == HttpStatusCode.RequestTimeout ||
        statusCode == HttpStatusCode.TooManyRequests ||
        (int)statusCode >= 500;

    private static TimeSpan Backoff(int attempt) =>
        TimeSpan.FromMilliseconds(Math.Min(2_000, 250 * Math.Pow(2, Math.Max(0, attempt - 1))));

    private static async Task DelayForRetryAsync(HttpResponseMessage response, int attempt, CancellationToken ct)
    {
        var delay = response.Headers.RetryAfter?.Delta ?? Backoff(attempt);
        if (delay > TimeSpan.FromSeconds(10))
            delay = TimeSpan.FromSeconds(10);

        await Task.Delay(delay, ct).ConfigureAwait(false);
    }

    private static string? TryGetRequestId(HttpResponseMessage response)
    {
        foreach (var header in new[] { "x-request-id", "apim-request-id", "request-id" })
        {
            if (response.Headers.TryGetValues(header, out var values))
                return values.FirstOrDefault();
        }

        return null;
    }
}

public sealed record AzureOpenAiTransportResponse(byte[] Body, int ElapsedMs, string? RequestId);

public sealed class AzureOpenAiRequestException : Exception
{
    public AzureOpenAiRequestException(HttpStatusCode statusCode, string? requestId)
        : base(BuildMessage(statusCode, requestId))
    {
        StatusCode = statusCode;
        RequestId = requestId;
    }

    public HttpStatusCode StatusCode { get; }
    public string? RequestId { get; }

    private static string BuildMessage(HttpStatusCode statusCode, string? requestId)
    {
        var suffix = string.IsNullOrWhiteSpace(requestId) ? string.Empty : $" RequestId={requestId}.";
        return $"Azure OpenAI returned HTTP {(int)statusCode} ({statusCode}).{suffix}";
    }
}
