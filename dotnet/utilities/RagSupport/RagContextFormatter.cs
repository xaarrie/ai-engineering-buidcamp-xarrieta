using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace RagSupport;

// Module lesson: formatting is the deterministic pre-generation boundary. Records are ordered with stable ordinal and text tie-breakers, Utf8JsonWriter owns escaping, and only explicit question/context placeholders are substituted.

/// <summary>Represents one retrieved chunk and optional source range to serialize into model context.</summary>
public sealed record RagRetrievalRecord(
    string ChunkId,
    string SourceIdentifier,
    int Ordinal,
    string Text,
    ChunkProvenance? Provenance = null);

/// <summary>Contains both escaped JSON context and the template-rendered prompt.</summary>
public sealed record RagFormattedContext(string ContextJson, string RenderedContext);

/// <summary>Serializes retrieval records deterministically and substitutes only the two supported template placeholders.</summary>
public static class RagContextFormatter
{
/// <summary>Requires literal <c>{question}</c> and <c>{context}</c> placeholders, then substitutes question text and escaped JSON.</summary>
    /// <remarks>Question text is inserted as supplied; JSON string escaping is delegated to <see cref="Utf8JsonWriter"/>.</remarks>
    public static RagFormattedContext Format(
        string question,
        IEnumerable<RagRetrievalRecord> retrievalRecords,
        string template)
    {
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(retrievalRecords);
        ArgumentNullException.ThrowIfNull(template);

        if (!template.Contains("{question}", StringComparison.Ordinal) ||
            !template.Contains("{context}", StringComparison.Ordinal))
        {
            throw new ArgumentException("Template must contain {question} and {context} placeholders.", nameof(template));
        }

        var contextJson = Serialize(retrievalRecords);
        var renderedContext = template
            .Replace("{question}", question, StringComparison.Ordinal)
            .Replace("{context}", contextJson, StringComparison.Ordinal);

        return new RagFormattedContext(contextJson, renderedContext);
    }

/// <summary>Sorts by source, ordinal, chunk ID, and text, then emits a JSON array with optional provenance.</summary>
    /// <remarks>The enumerable must not contain null records: ordering reads record properties before the loop's explicit null check, so a null element causes <see cref="NullReferenceException"/> during ordering. Null provenance is omitted.</remarks>
    public static string Serialize(IEnumerable<RagRetrievalRecord> retrievalRecords)
    {
        ArgumentNullException.ThrowIfNull(retrievalRecords);

        var orderedRecords = retrievalRecords
            .OrderBy(record => record.SourceIdentifier, StringComparer.Ordinal)
            .ThenBy(record => record.Ordinal)
            .ThenBy(record => record.ChunkId, StringComparer.Ordinal)
            .ThenBy(record => record.Text, StringComparer.Ordinal)
            .ToArray();

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.Default }))
        {
            writer.WriteStartArray();
            foreach (var record in orderedRecords)
            {
                ArgumentNullException.ThrowIfNull(record);
                writer.WriteStartObject();
                writer.WriteString("chunkId", record.ChunkId);
                writer.WriteString("sourceIdentifier", record.SourceIdentifier);
                writer.WriteNumber("ordinal", record.Ordinal);
                writer.WriteString("text", record.Text);

                if (record.Provenance is not null)
                {
                    writer.WritePropertyName("provenance");
                    writer.WriteStartObject();
                    writer.WriteString("sourceIdentifier", record.Provenance.SourceIdentifier);
                    writer.WriteNumber("startIndex", record.Provenance.StartIndex);
                    writer.WriteNumber("endIndex", record.Provenance.EndIndex);
                    writer.WriteString("label", record.Provenance.Label);
                    writer.WriteEndObject();
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
