namespace ApiTester.Rag.Models;

public sealed record RagAnswer(string Answer, IReadOnlyList<RagRetrievedChunk> Evidence);
