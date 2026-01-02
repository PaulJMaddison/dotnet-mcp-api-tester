using ApiTester.Rag.Embeddings;
using ApiTester.Rag.Models;
using ApiTester.Rag.Prompting;
using ApiTester.Rag.VectorStore;

namespace ApiTester.Rag.Answering;

public sealed class RagAnswerService
{
    private readonly IEmbeddingClient _embeddings;
    private readonly IVectorStore _store;
    private readonly RagPromptBuilder _prompt;
    private readonly IChatCompletionClient _chat;

    public RagAnswerService(IEmbeddingClient embeddings, IVectorStore store, RagPromptBuilder prompt, IChatCompletionClient chat)
    {
        _embeddings = embeddings;
        _store = store;
        _prompt = prompt;
        _chat = chat;
    }

    public async Task<RagAnswer> AnswerAsync(Guid projectId, string question, int topK, CancellationToken ct)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("projectId required", nameof(projectId));
        if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("question required", nameof(question));

        var qEmbedding = await _embeddings.EmbedAsync(question, ct).ConfigureAwait(false);

        var evidence = await _store.QueryAsync(
            projectId: projectId,
            embedding: qEmbedding,
            topK: Math.Clamp(topK, 1, 20),
            filters: null,
            ct: ct).ConfigureAwait(false);

        var userPrompt = _prompt.BuildUserPrompt(question, evidence);

        var answer = await _chat.CompleteAsync(_prompt.SystemPrompt, userPrompt, ct).ConfigureAwait(false);

        return new RagAnswer(answer, evidence);
    }
}
