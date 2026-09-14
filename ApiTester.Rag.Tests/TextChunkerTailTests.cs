using ApiTester.Rag.Chunking;

namespace ApiTester.Rag.Tests;

public sealed class TextChunkerTailTests
{
    [Fact]
    public void Chunk_FinalShortTailIsNotDropped()
    {
        var chunker = new TextChunker(new ChunkerOptions(MaxCharsPerChunk: 400, OverlapChars: 0, MinChunkChars: 150));
        var prefix = string.Join("\n", Enumerable.Range(0, 8).Select(i => $"GET /resource/{i} {new string('x', 55)}"));
        var tailMarker = "SECURITY_SCHEME_AT_END bearerAuth JWT";
        var text = prefix + "\n" + tailMarker;

        var chunks = chunker.Chunk(Guid.NewGuid(), "openapi", "spec", text);

        Assert.True(chunks.Count > 1);
        Assert.Contains(chunks, chunk => chunk.Text.Contains(tailMarker, StringComparison.Ordinal));
    }
}
