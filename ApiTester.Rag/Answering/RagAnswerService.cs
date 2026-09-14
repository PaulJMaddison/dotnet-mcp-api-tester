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
        _embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _prompt = prompt ?? throw new ArgumentNullException(nameof(prompt));
        _chat = chat ?? throw new ArgumentNullException(nameof(chat));
    }

    public async Task<RagAnswer> AnswerAsync(Guid projectId, string question, int topK, CancellationToken ct)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("projectId required", nameof(projectId));
        if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("question required", nameof(question));

        ct.ThrowIfCancellationRequested();
        var trimmedQuestion = question.Trim();
        var qEmbedding = await _embeddings.EmbedAsync(trimmedQuestion, ct).ConfigureAwait(false);

        var evidence = await _store.QueryAsync(
            projectId: projectId,
            embedding: qEmbedding,
            topK: Math.Clamp(topK, 1, 20),
            filters: null,
            ct: ct).ConfigureAwait(false);

        if (evidence.Count == 0)
        {
            return new RagAnswer(
                "I do not have indexed evidence for this project. Index an OpenAPI specification before asking grounded questions.",
                evidence);
        }

        var userPrompt = _prompt.BuildUserPrompt(trimmedQuestion, evidence);
        var answer = await _chat.CompleteAsync(_prompt.SystemPrompt, userPrompt, ct).ConfigureAwait(false);

        return new RagAnswer(answer, evidence);
    }
}
