using System.Text.Json;
using ApiTester.AI.Azure;
using ApiTester.Rag.Embeddings;

namespace ApiTester.McpServer.Rag;

public sealed class AzureOpenAiEmbeddingClient : IBatchEmbeddingClient
{
    private const int MaxBatchItems = 32;

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
        var result = await EmbedBatchAsync(new[] { text }, ct).ConfigureAwait(false);
        return result[0];
    }

    public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(texts);
        ct.ThrowIfCancellationRequested();
        if (texts.Count == 0) return Array.Empty<float[]>();

        var normalised = texts.Select(NormaliseInput).ToArray();
        var results = new List<float[]>(normalised.Length);
        var offset = 0;
        var batchCharLimit = Math.Min(_options.MaxInputChars, _options.MaxEmbeddingBatchChars);

        while (offset < normalised.Length)
        {
            ct.ThrowIfCancellationRequested();

            var batch = new List<string>(Math.Min(MaxBatchItems, normalised.Length - offset));
            var totalChars = 0;

            while (offset + batch.Count < normalised.Length && batch.Count < MaxBatchItems)
            {
                var candidate = normalised[offset + batch.Count];
                if (batch.Count > 0 && totalChars + candidate.Length > batchCharLimit)
                    break;

                batch.Add(candidate);
                totalChars += candidate.Length;
            }

            if (batch.Count == 0)
                batch.Add(normalised[offset]);

            var payload = new
            {
                model = _options.EmbeddingDeployment,
                input = batch
            };

            var response = await _transport.PostJsonAsync("embeddings", payload, ct).ConfigureAwait(false);
            var vectors = ParseBatch(response.Body, batch.Count);
            results.AddRange(vectors);
            offset += batch.Count;
        }

        return results;
    }

    private string NormaliseInput(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("Text is required for embedding.", nameof(text));

        var maxChars = Math.Min(_options.MaxInputChars, _options.MaxEmbeddingBatchChars);
        return text.Length <= maxChars
            ? text
            : text[..maxChars];
    }

    private static IReadOnlyList<float[]> ParseBatch(byte[] body, int expectedCount)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            throw new InvalidOperationException("Azure OpenAI returned no embedding data.");

        var indexed = new SortedDictionary<int, float[]>();
        var fallbackIndex = 0;

        foreach (var item in data.EnumerateArray())
        {
            if (!item.TryGetProperty("embedding", out var embedding) || embedding.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException("Azure OpenAI returned no embedding vector.");

            var vector = new float[embedding.GetArrayLength()];
            var valueIndex = 0;
            foreach (var value in embedding.EnumerateArray())
            {
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetSingle(out vector[valueIndex]))
                    throw new InvalidOperationException("Azure OpenAI returned an invalid embedding value.");
                valueIndex++;
            }

            if (vector.Length == 0)
                throw new InvalidOperationException("Azure OpenAI returned an empty embedding vector.");

            var index = item.TryGetProperty("index", out var indexElement) && indexElement.TryGetInt32(out var parsedIndex)
                ? parsedIndex
                : fallbackIndex;
            fallbackIndex++;

            if (!indexed.TryAdd(index, vector))
                throw new InvalidOperationException($"Azure OpenAI returned duplicate embedding index {index}.");
        }

        if (indexed.Count != expectedCount || indexed.Keys.Any(index => index < 0 || index >= expectedCount))
            throw new InvalidOperationException(
                $"Azure OpenAI returned {indexed.Count} embeddings for {expectedCount} inputs.");

        return Enumerable.Range(0, expectedCount)
            .Select(index => indexed.TryGetValue(index, out var vector)
                ? vector
                : throw new InvalidOperationException($"Azure OpenAI omitted embedding index {index}."))
            .ToList();
    }
}
