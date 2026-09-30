using System.Security.Cryptography;
using System.Text;

namespace RagSupport;

// Module lesson: windows count UTF-16 characters, not model tokens. Step creates overlap and intentionally permits a final partial window. Provenance preserves source offsets and labels; the SHA-256 ID combines source, ordinal, start, and text for stable identity.

/// <summary>Records the source and UTF-16 character range from which a chunk was cut.</summary>
/// <param name="StartIndex">Inclusive character index.</param>
/// <param name="EndIndex">Exclusive character index.</param>
/// <param name="Label">Optional human-readable source label; null is preserved.</param>
public sealed record ChunkProvenance(string SourceIdentifier, int StartIndex, int EndIndex, string? Label);

/// <summary>A character-window result with stable identity, ordinal position, text, and provenance.</summary>
public sealed record RagChunk(
    string ChunkId,
    string SourceIdentifier,
    int Ordinal,
    string Text,
    ChunkProvenance Provenance);

/// <summary>Controls UTF-16 character window size and start step; it does not count model tokens.</summary>
/// <param name="Size">Maximum characters per window.</param>
/// <param name="Step">Distance between consecutive starts; values below size create overlap.</param>
public sealed record CharacterWindowChunkingOptions(int Size, int Step)
{
/// <summary>Rejects nonpositive size or step so the chunk loop always advances with bounded windows.</summary>
    public void Validate()
    {
        if (Size <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Size), "Size must be positive.");
        }

        if (Step <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(Step), "Step must be positive.");
        }
    }
}

/// <summary>Cuts deterministic UTF-16 character windows and attaches source provenance.</summary>
public static class CharacterWindowChunker
{
/// <summary>Creates windows starting at 0, Step, and so on; overlap is intentional and the final window may be partial.</summary>
    /// <remarks>An empty text returns an empty read-only list. IDs are lowercase SHA-256 hashes of source, ordinal, start, and window text.</remarks>
    public static IReadOnlyList<RagChunk> Chunk(
        string sourceIdentifier,
        string text,
        CharacterWindowChunkingOptions options,
        string? provenanceLabel = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceIdentifier);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        if (text.Length == 0)
        {
            return [];
        }

        var chunks = new List<RagChunk>();
        for (var startIndex = 0; startIndex < text.Length; startIndex += options.Step)
        {
            var length = Math.Min(options.Size, text.Length - startIndex);
            var ordinal = chunks.Count;
            var window = text.Substring(startIndex, length);
            var provenance = new ChunkProvenance(sourceIdentifier, startIndex, startIndex + length, provenanceLabel);
            chunks.Add(new RagChunk(CreateChunkId(sourceIdentifier, ordinal, startIndex, window), sourceIdentifier, ordinal, window, provenance));
        }

        return chunks.AsReadOnly();
    }

    private static string CreateChunkId(string sourceIdentifier, int ordinal, int startIndex, string text)
    {
        var identity = string.Concat(sourceIdentifier, "\n", ordinal, "\n", startIndex, "\n", text);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
