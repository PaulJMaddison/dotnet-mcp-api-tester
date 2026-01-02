using System.Security.Cryptography;
using System.Text;
using ApiTester.Rag.Models;

namespace ApiTester.Rag.Chunking;

public sealed class TextChunker
{
    private readonly ChunkerOptions _o;

    public TextChunker(ChunkerOptions options)
    {
        if (options.MaxCharsPerChunk < 400) throw new ArgumentOutOfRangeException(nameof(options.MaxCharsPerChunk));
        if (options.OverlapChars < 0) throw new ArgumentOutOfRangeException(nameof(options.OverlapChars));
        if (options.OverlapChars >= options.MaxCharsPerChunk) throw new ArgumentOutOfRangeException(nameof(options.OverlapChars));
        if (options.MinChunkChars < 50) throw new ArgumentOutOfRangeException(nameof(options.MinChunkChars));

        _o = options;
    }

    public IReadOnlyList<RagChunk> Chunk(
        Guid projectId,
        string sourceType,
        string sourceId,
        string text,
        IReadOnlyDictionary<string, string>? metadata = null,
        DateTime? createdUtc = null)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("projectId required", nameof(projectId));
        if (string.IsNullOrWhiteSpace(sourceType)) throw new ArgumentException("sourceType required", nameof(sourceType));
        if (string.IsNullOrWhiteSpace(sourceId)) throw new ArgumentException("sourceId required", nameof(sourceId));

        text = Normalise(text);
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<RagChunk>();

        metadata ??= new Dictionary<string, string>();
        var created = createdUtc ?? DateTime.UtcNow;

        var chunks = new List<RagChunk>();
        var idx = 0;
        var chunkIndex = 0;

        while (idx < text.Length)
        {
            var remaining = text.Length - idx;
            var take = Math.Min(_o.MaxCharsPerChunk, remaining);

            var candidate = text.Substring(idx, take);
            var cut = FindBestCut(candidate);

            if (cut < _o.MinChunkChars && remaining > _o.MinChunkChars)
                cut = candidate.Length;

            var chunkText = candidate.Substring(0, cut).Trim();
            if (chunkText.Length >= _o.MinChunkChars)
            {
                var chunkId = $"{sourceType}:{sourceId}:{chunkIndex:D4}";
                var hash = Sha256Hex(chunkText);

                chunks.Add(new RagChunk(
                    ProjectId: projectId,
                    SourceType: sourceType,
                    SourceId: sourceId,
                    ChunkId: chunkId,
                    Text: chunkText,
                    ContentHash: hash,
                    CreatedUtc: created,
                    Metadata: metadata));

                chunkIndex++;
            }

            if (idx + cut >= text.Length) break;

            var step = Math.Max(1, cut - _o.OverlapChars);
            idx += step;
        }

        return chunks;
    }

    private static string Normalise(string text)
    {
        text ??= string.Empty;
        text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        while (text.Contains("\n\n\n")) text = text.Replace("\n\n\n", "\n\n");
        return text.Trim();
    }

    private static int FindBestCut(string candidate)
    {
        if (candidate.Length < 50) return candidate.Length;

        var start = (int)(candidate.Length * 0.75);
        var slice = candidate.AsSpan(start);

        var dn = slice.LastIndexOf("\n\n");
        if (dn >= 0) return start + dn + 2;

        var sn = slice.LastIndexOf('\n');
        if (sn >= 0) return start + sn + 1;

        var period = slice.LastIndexOf(". ");
        if (period >= 0) return start + period + 2;

        return candidate.Length;
    }

    private static string Sha256Hex(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
