namespace ApiTester.Rag.Embeddings;

public interface IEmbeddingClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}
