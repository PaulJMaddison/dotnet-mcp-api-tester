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

    public async Task<RagAnswer> AnswerAsync(Guid scopeId, string question, int topK, CancellationToken ct)
    {
        if (scopeId == Guid.Empty) throw new ArgumentException("scopeId is required.", nameof(scopeId));
        if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("question is required.", nameof(question));

        var queryVector = await _embeddings.EmbedAsync(question.Trim(), ct).ConfigureAwait(false);
        var evidence = await _store.QueryAsync(
            scopeId,
            queryVector,
            Math.Clamp(topK, 1, 20),
            filters: null,
            ct).ConfigureAwait(false);

        if (evidence.Count == 0)
            return new RagAnswer("I do not have indexed OpenAPI evidence for the loaded API.", evidence);

        var userPrompt = _prompt.BuildUserPrompt(question.Trim(), evidence);
        var answer = await _chat.CompleteAsync(_prompt.SystemPrompt, userPrompt, ct).ConfigureAwait(false);
        return new RagAnswer(answer, evidence);
    }
}
