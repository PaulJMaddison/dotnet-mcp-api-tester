using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ApiTester.AI.Cost;
using Azure.Core;
using Azure.Identity;

namespace ApiTester.AI.Azure;

public sealed class AzureOpenAiClient : IAiClient
{
    private static readonly string[] TokenScopes = ["https://ai.azure.com/.default"];
    private readonly HttpClient _httpClient;
    private readonly AzureOpenAiOptions _options;
    private readonly TokenCredential _credential;

    public AzureOpenAiClient(HttpClient httpClient, AzureOpenAiOptions options, TokenCredential? credential = null)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);

        if (!options.IsConfigured)
        {
            throw new InvalidOperationException(
                $"Azure OpenAI is not configured. Set {AzureOpenAiOptions.SectionName}:Endpoint, ChatDeployment, and Authentication=DefaultAzureCredential.");
        }

        _httpClient = httpClient;
        _options = options;
        _credential = credential ?? new DefaultAzureCredential();
    }

    public async Task<AiResponse> GetResponseAsync(AiPrompt prompt, CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var token = await _credential.GetTokenAsync(new TokenRequestContext(TokenScopes), ct);
        var endpoint = _options.Endpoint.TrimEnd('/') + "/chat/completions";
        var requestBody = new
        {
            model = _options.ChatDeployment,
            messages = new[]
            {
                new { role = "system", content = prompt.System },
                new { role = "user", content = prompt.User }
            },
            temperature = 0.2,
            max_tokens = Math.Clamp(_options.MaxOutputTokens, 1, 16_384)
        };

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds)));

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        request.Content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Azure OpenAI returned HTTP {(int)response.StatusCode} ({response.ReasonPhrase}).",
                null,
                response.StatusCode);
        }

        await using var responseStream = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: timeout.Token);
        var root = document.RootElement;
        var choices = root.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
            throw new InvalidOperationException("Azure OpenAI returned no choices.");

        var content = choices[0].GetProperty("message").GetProperty("content").GetString();
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Azure OpenAI returned empty content.");

        var inputTokens = 0;
        var outputTokens = 0;
        if (root.TryGetProperty("usage", out var usage))
        {
            inputTokens = ReadInt(usage, "prompt_tokens");
            outputTokens = ReadInt(usage, "completion_tokens");
        }

        stopwatch.Stop();
        var model = string.IsNullOrWhiteSpace(_options.ModelName) ? _options.ChatDeployment : _options.ModelName;
        return new AiResponse(
            content,
            new AiUsage(inputTokens, outputTokens),
            (int)stopwatch.ElapsedMilliseconds,
            model,
            AiCostCalculator.Estimate(model, inputTokens, outputTokens));
    }

    private static int ReadInt(JsonElement parent, string propertyName)
        => parent.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result) ? result : 0;
}
