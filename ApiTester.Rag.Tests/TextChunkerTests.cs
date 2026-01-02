using ApiTester.Rag.Chunking;
using Xunit;

namespace ApiTester.Rag.Tests;

public sealed class TextChunkerTests
{
    [Fact]
    public void Chunker_SplitsText_AndProducesHashes()
    {
        var chunker = new TextChunker(new ChunkerOptions(MaxCharsPerChunk: 500, OverlapChars: 120, MinChunkChars: 150));

        var text = string.Join("\n\n", Enumerable.Range(1, 30).Select(i => $"Section {i}\n" + new string('x', 120)));

        var chunks = chunker.Chunk(Guid.NewGuid(), "openapi", "spec-1", text);

        Assert.True(chunks.Count >= 3);
        Assert.All(chunks, c => Assert.False(string.IsNullOrWhiteSpace(c.ContentHash)));
        Assert.All(chunks, c => Assert.True(c.Text.Length <= 500));
    }

    [Fact]
    public void Chunker_EmptyText_ReturnsEmpty()
    {
        var chunker = new TextChunker(new ChunkerOptions());
        var chunks = chunker.Chunk(Guid.NewGuid(), "openapi", "spec-1", "   ");
        Assert.Empty(chunks);
    }
}
