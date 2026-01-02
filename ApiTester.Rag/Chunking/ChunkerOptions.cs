namespace ApiTester.Rag.Chunking;

public sealed record ChunkerOptions(
    int MaxCharsPerChunk = 1400,
    int OverlapChars = 200,
    int MinChunkChars = 250);
