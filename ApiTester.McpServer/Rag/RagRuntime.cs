using ApiTester.Rag.Answering;
using ApiTester.Rag.Embeddings;
using ApiTester.Rag.Indexing;
using ApiTester.Rag.Prompting;
using ApiTester.Rag.VectorStore;

namespace ApiTester.McpServer.Rag;

public sealed class RagRuntime
{
    public RagIndexer Indexer { get; }
    public RagAnswerService Answerer { get; }

    public RagRuntime(IChatCompletionClient chat, IEmbeddingClient embeddings, InMemoryVectorStore store)
    {
        Indexer = new RagIndexer(embeddings, store);
        Answerer = new RagAnswerService(embeddings, store, new RagPromptBuilder(), chat);
    }
}
