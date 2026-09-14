using System.Text.Json;
using ApiTester.AI.Cost;

namespace ApiTester.AI.Azure;

public sealed class AzureOpenAiClient : IAiClient
{
    private readonly AzureOpenAiTransport _transport;
    private readonly AzureOpenAiOptions _options;

    public AzureOpenAiClient(AzureOpenAiTransport transport, AzureOpenAiOptions options)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.ValidateChat();
    }

    public async Task<AiResponse> GetResponseAsync(AiPrompt prompt, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        ct.ThrowIfCancellationRequested();

        var system = Truncate(prompt.System, _options.MaxInputChars);
        var remaining = Math.Max(1, _options.MaxInputChars - system.Length);
        var user = Truncate(prompt.User, remaining);

        var payload = new Dictionary<string, object?>
        {
            ["model"] = _options.ChatDeployment,
            ["messages"] = new object[]
            {
                new { role = "system", content = system },
                new { role = "user", content = user }
            }
        };

        if (_options.MaxCompletionTokens > 0)
            payload["max_completion_tokens"] = _options.MaxCompletionTokens;

        var response = await _transport.PostJsonAsync("chat/completions", payload, ct).ConfigureAwait(false);

        using var document = JsonDocument.Parse(response.Body);
        var root = document.RootElement;

        if (!root.TryGetProperty("choices", out var choices) ||
            choices.ValueKind != JsonValueKind.Array ||
            choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Azure OpenAI returned no chat choices.");
        }

        var firstChoice = choices[0];
        if (!firstChoice.TryGetProperty("message", out var message) ||
            !message.TryGetProperty("content", out var contentElement))
        {
            throw new InvalidOperationException("Azure OpenAI response did not contain message content.");
        }

        var content = ReadContent(contentElement);
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("Azure OpenAI returned empty message content.");

        var usage = ParseUsage(root);

        return new AiResponse(
            Content: content,
            Usage: usage,
            ElapsedMs: response.ElapsedMs,
            Model: _options.ChatDeployment,
            Cost: AiCostCalculator.Estimate(_options.ChatDeployment, usage.InputTokens, usage.OutputTokens));
    }

    private static string ReadContent(JsonElement content)
    {
        if (content.ValueKind == JsonValueKind.String)
            return content.GetString() ?? string.Empty;

        if (content.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var parts = new List<string>();
        foreach (var item in content.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var text = item.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    parts.Add(text);
                continue;
            }

            if (item.ValueKind == JsonValueKind.Object &&
                item.TryGetProperty("text", out var textElement) &&
                textElement.ValueKind == JsonValueKind.String)
            {
                var text = textElement.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                    parts.Add(text);
            }
        }

        return string.Join(Environment.NewLine, parts);
    }

    private static AiUsage ParseUsage(JsonElement root)
    {
        if (!root.TryGetProperty("usage", out var usage) || usage.ValueKind != JsonValueKind.Object)
            return new AiUsage(0, 0);

        var input = ReadInt(usage, "prompt_tokens", "input_tokens");
        var output = ReadInt(usage, "completion_tokens", "output_tokens");
        return new AiUsage(input, output);
    }

    private static int ReadInt(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (element.TryGetProperty(name, out var value) && value.TryGetInt32(out var result))
                return result;
        }

        return 0;
    }

    private static string Truncate(string? value, int maxChars)
    {
        value ??= string.Empty;
        if (value.Length <= maxChars)
            return value;

        return value[..maxChars];
    }
}
