using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ApiTester.Rag.Embeddings;

/// <summary>
/// Dependency-free local fallback for tests and offline demos.
/// This is feature hashing over normalised terms, not a semantic model embedding.
/// Texts sharing important API terms therefore remain comparable while production
/// deployments can replace this implementation with a real embedding provider.
/// </summary>
public sealed class DeterministicHashEmbeddingClient : IEmbeddingClient
{
    private static readonly Regex TokenPattern = new(
        @"[A-Za-z0-9_./{}:-]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly int _dims;

    public DeterministicHashEmbeddingClient(int dims = 512)
    {
        if (dims < 32) throw new ArgumentOutOfRangeException(nameof(dims));
        _dims = dims;
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var output = new float[_dims];
        if (string.IsNullOrWhiteSpace(text))
            return Task.FromResult(output);

        var tokens = TokenPattern.Matches(text.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(token => token.Length > 1)
            .ToArray();

        foreach (var token in tokens)
        {
            ct.ThrowIfCancellationRequested();
            AddFeature(output, token, 1f);

            // API identifiers often carry meaning in their fragments: for example
            // getCustomerById, /customers/{id}, customer_id and application/json.
            foreach (var part in SplitIdentifier(token))
            {
                if (!string.Equals(part, token, StringComparison.Ordinal))
                    AddFeature(output, part, 0.5f);
            }
        }

        Normalise(output);
        return Task.FromResult(output);
    }

    private void AddFeature(float[] output, string token, float weight)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        var bucket = (int)(BitConverter.ToUInt32(bytes, 0) % (uint)_dims);
        var sign = (bytes[4] & 1) == 0 ? 1f : -1f;
        output[bucket] += sign * weight;
    }

    private static IEnumerable<string> SplitIdentifier(string token)
    {
        var buffer = new StringBuilder();
        foreach (var ch in token)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer.Append(ch);
                continue;
            }

            if (buffer.Length > 1)
                yield return buffer.ToString();
            buffer.Clear();
        }

        if (buffer.Length > 1)
            yield return buffer.ToString();
    }

    private static void Normalise(float[] vector)
    {
        var normSquared = 0f;
        for (var i = 0; i < vector.Length; i++)
            normSquared += vector[i] * vector[i];

        if (normSquared <= 0f)
            return;

        var norm = (float)Math.Sqrt(normSquared);
        for (var i = 0; i < vector.Length; i++)
            vector[i] /= norm;
    }
}
