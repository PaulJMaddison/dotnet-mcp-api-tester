using System.Security.Cryptography;
using System.Text;

namespace ApiTester.Rag.Embeddings;

public sealed class DeterministicHashEmbeddingClient : IEmbeddingClient
{
    private readonly int _dims;

    public DeterministicHashEmbeddingClient(int dims = 256)
    {
        if (dims < 32) throw new ArgumentOutOfRangeException(nameof(dims));
        _dims = dims;
    }

    public Task<float[]> EmbedAsync(string text, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        text ??= string.Empty;
        var bytes = Encoding.UTF8.GetBytes(text);

        var output = new float[_dims];
        var current = SHA256.HashData(bytes);

        var i = 0;
        while (i < _dims)
        {
            current = SHA256.HashData(current);
            for (var j = 0; j < current.Length && i < _dims; j++, i++)
            {
                output[i] = ((current[j] / 255f) * 2f) - 1f;
            }
        }

        var norm = 0f;
        for (var k = 0; k < output.Length; k++) norm += output[k] * output[k];
        norm = (float)Math.Sqrt(norm);

        if (norm > 0f)
        {
            for (var k = 0; k < output.Length; k++) output[k] /= norm;
        }

        return Task.FromResult(output);
    }
}
