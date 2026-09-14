namespace ApiTester.Rag.Embeddings;

public interface IEmbeddingClient
{
    Task<float[]> EmbedAsync(string text, CancellationToken ct);
}

public interface IBatchEmbeddingClient : IEmbeddingClient
{
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct);
}
