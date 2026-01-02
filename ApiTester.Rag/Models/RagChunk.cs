namespace ApiTester.Rag.Models;

public sealed record RagChunk(
    Guid ProjectId,
    string SourceType,
    string SourceId,
    string ChunkId,
    string Text,
    string ContentHash,
    DateTime CreatedUtc,
    IReadOnlyDictionary<string, string> Metadata);
