using System.Text.Json;
using ApiTester.AI.Azure;
using ApiTester.Rag.Embeddings;

namespace ApiTester.McpServer.Rag;

public sealed class AzureOpenAiEmbeddingClient : IEmbeddingClient
{
    private readonly AzureOpenAiTransport _transport;
    private readonly AzureOpenAiOptions _options;

    public AzureOpenAiEmbeddingClient(AzureOpenAiTransport transport, AzureOpenAiOptions options)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.ValidateEmbedding();
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required for embedding.", nameof(text));

        ct.ThrowIfCancellationRequested();

        var input = text.Length <= _options.MaxInputChars
            ? text
            : text[.._options.MaxInputChars];

        var payload = new
        {
            model = _options.EmbeddingDeployment,
            input
        };

        var response = await _transport.PostJsonAsync("embeddings", payload, ct).ConfigureAwait(false);

        using var document = JsonDocument.Parse(response.Body);
        var root = document.RootElement;

        if (!root.TryGetProperty("data", out var data) ||
            data.ValueKind != JsonValueKind.Array ||
            data.GetArrayLength() == 0 ||
            !data[0].TryGetProperty("embedding", out var embedding) ||
            embedding.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException("Azure OpenAI returned no embedding vector.");
        }

        var values = new float[embedding.GetArrayLength()];
        var index = 0;
        foreach (var value in embedding.EnumerateArray())
        {
            if (!value.TryGetSingle(out values[index]))
                throw new InvalidOperationException("Azure OpenAI returned an invalid embedding value.");

            index++;
        }

        if (values.Length == 0)
            throw new InvalidOperationException("Azure OpenAI returned an empty embedding vector.");

        return values;
    }
}
