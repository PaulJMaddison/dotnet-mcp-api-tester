using ApiTester.Rag.Chunking;

namespace ApiTester.Rag.Tests;

public sealed class TextChunkerEdgeCaseTests
{
    [Fact]
    public void Constructor_NullOptions_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new TextChunker(null!));
    }

    [Theory]
    [InlineData(399, 0, 50)]
    [InlineData(500, -1, 50)]
    [InlineData(500, 500, 50)]
    [InlineData(500, 0, 49)]
    [InlineData(500, 0, 501)]
    public void Constructor_InvalidBoundaries_Throw(int max, int overlap, int min)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new TextChunker(new ChunkerOptions(max, overlap, min)));
    }

    [Fact]
    public void Chunk_EmptyProjectId_Throws()
    {
        var chunker = new TextChunker(new ChunkerOptions());
        Assert.Throws<ArgumentException>(() =>
            chunker.Chunk(Guid.Empty, "openapi", "spec", "valid source text"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Chunk_InvalidSourceType_Throws(string? sourceType)
    {
        var chunker = new TextChunker(new ChunkerOptions());
        Assert.Throws<ArgumentException>(() =>
            chunker.Chunk(Guid.NewGuid(), sourceType!, "spec", "valid source text"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Chunk_InvalidSourceId_Throws(string? sourceId)
    {
        var chunker = new TextChunker(new ChunkerOptions());
        Assert.Throws<ArgumentException>(() =>
            chunker.Chunk(Guid.NewGuid(), "openapi", sourceId!, "valid source text"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\r\n")]
    public void Chunk_EmptyOrWhitespaceText_ReturnsEmpty(string? text)
    {
        var chunker = new TextChunker(new ChunkerOptions());
        var chunks = chunker.Chunk(Guid.NewGuid(), "openapi", "spec", text!);
        Assert.Empty(chunks);
    }

    [Fact]
    public void Chunk_SmallValidDocument_ProducesSingleChunk()
    {
        var chunker = new TextChunker(new ChunkerOptions(MinChunkChars: 250));
        const string smallSpec = "{\"openapi\":\"3.0.1\",\"paths\":{\"/ping\":{\"get\":{}}}}";

        var chunks = chunker.Chunk(Guid.NewGuid(), "openapi", "small", smallSpec);

        var chunk = Assert.Single(chunks);
        Assert.Equal(smallSpec, chunk.Text);
        Assert.Equal("openapi:small:0000", chunk.ChunkId);
        Assert.False(string.IsNullOrWhiteSpace(chunk.ContentHash));
    }

    [Fact]
    public void Chunk_ExactlyMinimumLength_IsIndexed()
    {
        var chunker = new TextChunker(new ChunkerOptions(MaxCharsPerChunk: 500, MinChunkChars: 250));
        var text = new string('a', 250);

        var chunk = Assert.Single(chunker.Chunk(Guid.NewGuid(), "openapi", "spec", text));

        Assert.Equal(250, chunk.Text.Length);
    }

    [Fact]
    public void Chunk_NormalisesLineEndingsAndExcessBlankLines()
    {
        var chunker = new TextChunker(new ChunkerOptions(MinChunkChars: 50));
        var text = "line1\r\n\r\n\r\nline2\rline3";

        var chunk = Assert.Single(chunker.Chunk(Guid.NewGuid(), "openapi", "spec", text));

        Assert.DoesNotContain("\r", chunk.Text);
        Assert.DoesNotContain("\n\n\n", chunk.Text);
        Assert.Contains("line1\n\nline2\nline3", chunk.Text);
    }

    [Fact]
    public void Chunk_PreservesContextFieldsAndMetadata()
    {
        var projectId = Guid.NewGuid();
        var created = new DateTime(2026, 9, 14, 12, 0, 0, DateTimeKind.Utc);
        var metadata = new Dictionary<string, string>
        {
            ["Title"] = "Customer API",
            ["Version"] = "2.1"
        };
        var chunker = new TextChunker(new ChunkerOptions(MinChunkChars: 50));

        var chunk = Assert.Single(chunker.Chunk(
            projectId,
            "openapi",
            "spec-123",
            "GET /customers returns a customer record with id and name.",
            metadata,
            created));

        Assert.Equal(projectId, chunk.ProjectId);
        Assert.Equal("openapi", chunk.SourceType);
        Assert.Equal("spec-123", chunk.SourceId);
        Assert.Equal(created, chunk.CreatedUtc);
        Assert.Same(metadata, chunk.Metadata);
        Assert.Equal("Customer API", chunk.Metadata["Title"]);
    }

    [Fact]
    public void Chunk_SameInput_ProducesStableChunkIdsAndHashes()
    {
        var chunker = new TextChunker(new ChunkerOptions(MaxCharsPerChunk: 500, OverlapChars: 100, MinChunkChars: 100));
        var projectId = Guid.NewGuid();
        var text = string.Join("\n\n", Enumerable.Range(1, 20).Select(i => $"section-{i} {new string((char)('a' + i % 20), 80)}"));

        var first = chunker.Chunk(projectId, "openapi", "spec", text);
        var second = chunker.Chunk(projectId, "openapi", "spec", text);

        Assert.Equal(first.Select(c => c.ChunkId), second.Select(c => c.ChunkId));
        Assert.Equal(first.Select(c => c.ContentHash), second.Select(c => c.ContentHash));
        Assert.Equal(first.Select(c => c.Text), second.Select(c => c.Text));
    }

    [Fact]
    public void Chunk_DifferentContent_ChangesContentHash()
    {
        var chunker = new TextChunker(new ChunkerOptions(MinChunkChars: 50));
        var projectId = Guid.NewGuid();

        var a = Assert.Single(chunker.Chunk(projectId, "openapi", "spec", "GET /customers returns all customers."));
        var b = Assert.Single(chunker.Chunk(projectId, "openapi", "spec", "POST /customers creates a customer."));

        Assert.NotEqual(a.ContentHash, b.ContentHash);
    }

    [Fact]
    public void Chunk_LargeInput_RespectsMaximumAndSequentialIds()
    {
        var chunker = new TextChunker(new ChunkerOptions(MaxCharsPerChunk: 400, OverlapChars: 80, MinChunkChars: 100));
        var text = string.Join("\n", Enumerable.Range(0, 50).Select(i => $"operation {i}: GET /resource/{i} {new string('x', 50)}"));

        var chunks = chunker.Chunk(Guid.NewGuid(), "openapi", "catalog", text);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.InRange(c.Text.Length, 1, 400));
        for (var i = 0; i < chunks.Count; i++)
            Assert.Equal($"openapi:catalog:{i:D4}", chunks[i].ChunkId);
    }
}
