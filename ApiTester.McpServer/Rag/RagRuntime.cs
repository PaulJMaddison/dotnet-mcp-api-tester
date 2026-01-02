using ApiTester.Rag.Answering;
using ApiTester.Rag.Chunking;
using ApiTester.Rag.Embeddings;
using ApiTester.Rag.Indexing;
using ApiTester.Rag.Prompting;
using ApiTester.Rag.VectorStore;

namespace ApiTester.McpServer.Rag;

public sealed class RagRuntime
{
    public TextChunker Chunker { get; }
    public RagIndexer Indexer { get; }
    public RagAnswerService Answerer { get; }

    public RagRuntime(IChatCompletionClient chat, IEmbeddingClient embeddings, InMemoryVectorStore store)
    {
        Chunker = new TextChunker(new ChunkerOptions());
        Indexer = new RagIndexer(embeddings, store);
        Answerer = new RagAnswerService(embeddings, store, new RagPromptBuilder(), chat);
    }
}
