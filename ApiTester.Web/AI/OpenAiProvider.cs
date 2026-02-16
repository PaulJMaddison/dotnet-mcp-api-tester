using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ApiTester.Web.AI;

public sealed class OpenAiProvider : IAiProvider
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OpenAiProviderOptions _options;
    private readonly ILogger<OpenAiProvider> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly object _circuitLock = new();
    private int _consecutiveFailures;
    private DateTimeOffset? _circuitOpenedUntil;

    public OpenAiProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<OpenAiProviderOptions> options,
        ILogger<OpenAiProvider> logger,
        TimeProvider timeProvider)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public Task<AiResult> ExplainApiAsync(string spec, string operationId, CancellationToken ct)
        => CompleteAsync($"Explain operation {operationId}", spec, _options.DefaultModel, ct);

    public Task<AiResult> SuggestEdgeCasesAsync(string spec, string operationId, CancellationToken ct)
        => CompleteAsync($"Suggest edge cases for operation {operationId}", spec, _options.DefaultModel, ct);

    public Task<AiResult> SummariseRunAsync(string runId, string runContext, CancellationToken ct)
        => CompleteAsync($"Summarise run {runId}", runContext, _options.ProModel, ct);

    public Task<AiResult> SuggestFixesAsync(string runId, string runContext, CancellationToken ct)
        => CompleteAsync($"Suggest improvements for run {runId}", runContext, _options.ProModel, ct);

    private async Task<AiResult> CompleteAsync(string taskPrompt, string context, string model, CancellationToken ct)
    {
        EnsureCircuitClosed();
        var apiKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("OpenAI API key is not configured.");

        var trimmedContext = Truncate(context, _options.MaxInputChars);
        var prompt = $"{taskPrompt}. Return valid JSON only.\n\nContext:\n{trimmedContext}";
        var maxTokens = Math.Clamp(_options.MaxOutputChars / 4, 256, 2048);

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        linkedCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds)));

        var requestBody = new
        {
            model,
            messages = new[]
            {
                new { role = "system", content = "You are a strict API testing assistant." },
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            max_tokens = maxTokens
        };

        var attempts = Math.Max(1, _options.MaxRetries + 1);
        Exception? lastError = null;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            try
            {
                var client = _httpClientFactory.CreateClient(nameof(OpenAiProvider));
                client.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
                client.Timeout = Timeout.InfiniteTimeSpan;
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

                using var request = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
                {
                    Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json")
                };

                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
                if (IsTransient(response.StatusCode))
                {
                    var body = await response.Content.ReadAsStringAsync(linkedCts.Token);
                    throw new HttpRequestException($"Transient OpenAI failure ({(int)response.StatusCode}): {body}");
                }

                response.EnsureSuccessStatusCode();
                var bytes = await response.Content.ReadAsByteArrayAsync(linkedCts.Token);
                if (bytes.Length > _options.MaxResponseBytes)
                    throw new InvalidOperationException($"OpenAI response exceeded max allowed size ({_options.MaxResponseBytes} bytes).");

                var content = ParseContent(bytes);
                RecordSuccess();
                return new AiResult(Truncate(content, _options.MaxOutputChars), model);
            }
            catch (Exception ex) when (attempt < attempts)
            {
                lastError = ex;
                _logger.LogWarning(ex, "OpenAI call attempt {Attempt} failed.", attempt);
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), linkedCts.Token);
            }
            catch (Exception ex)
            {
                lastError = ex;
                break;
            }
        }

        RecordFailure(lastError);
        throw new InvalidOperationException("OpenAI request failed after retries.", lastError);
    }

    private static string ParseContent(byte[] bytes)
    {
        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;
        var choices = root.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
            throw new InvalidOperationException("OpenAI returned no choices.");

        var content = choices[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("OpenAI returned empty content.");

        return content;
    }

    private static bool IsTransient(HttpStatusCode statusCode)
        => statusCode == HttpStatusCode.TooManyRequests || (int)statusCode >= 500;

    private void EnsureCircuitClosed()
    {
        lock (_circuitLock)
        {
            if (_circuitOpenedUntil is not null && _circuitOpenedUntil > _timeProvider.GetUtcNow())
                throw new InvalidOperationException("OpenAI circuit breaker is open.");

            if (_circuitOpenedUntil is not null && _circuitOpenedUntil <= _timeProvider.GetUtcNow())
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

    private void RecordFailure(Exception? ex)
    {
        lock (_circuitLock)
        {
            _consecutiveFailures++;
            if (_consecutiveFailures >= Math.Max(1, _options.CircuitBreakerFailureThreshold))
            {
                _circuitOpenedUntil = _timeProvider.GetUtcNow().AddSeconds(Math.Max(5, _options.CircuitBreakerBreakSeconds));
                _logger.LogWarning(ex, "OpenAI circuit opened until {OpenUntil} after {Failures} consecutive failures.", _circuitOpenedUntil, _consecutiveFailures);
            }
        }
    }

    private static string Truncate(string value, int maxChars)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxChars)
            return value;

        return value[..maxChars];
    }
}
