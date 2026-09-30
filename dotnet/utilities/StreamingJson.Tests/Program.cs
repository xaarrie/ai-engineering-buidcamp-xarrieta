// Fixture lesson: deliberately split bytes test Feed lifecycle, UTF-8 and Unicode escape state, nested callback order, array captures, and exactly-once malformed-input errors.

using System.Text;
using StreamingJson;

var assertions = new (string Name, Action Test)[]
{
    ("emits string chunks before a closing quote", StringChunkImmediacy),
    ("decodes escaped quotes and backslashes", EscapedCharacters),
    ("decodes Unicode surrogate pairs", UnicodeSurrogatePair),
    ("handles UTF-8 split across chunks", SplitUtf8),
    ("preserves nested callback ordering", NestedOrdering),
    ("preserves multi-level nested-array callback ordering", MultiLevelNestedArrayOrdering),
    ("emits completed array items", ArrayItems),
    ("accepts one-byte chunks", OneByteChunks),
    ("reports malformed final input", MalformedFinalInput),
    ("rejects incomplete UTF-8 final input split across chunks", IncompleteUtf8FinalInput),
    ("rejects incomplete Unicode escape final input split across chunks", IncompleteUnicodeEscapeFinalInput),
    ("rejects unpaired surrogate final input split across chunks", UnpairedSurrogateFinalInput)
};

try
{
    foreach (var assertion in assertions)
    {
        assertion.Test();
    }

    Console.WriteLine($"SUCCESS: {assertions.Length} StreamingJson assertions passed.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL: {exception.Message}");
    return 1;
}

static void StringChunkImmediacy()
{
    var handler = new RecordingHandler();
    var parser = new StreamingJsonParser(handler);
    parser.Feed(Encoding.UTF8.GetBytes("{\"text\":\"hel"));
    Require(handler.Events.SequenceEqual(["field:text", "chunk:text:h", "chunk:text:e", "chunk:text:l"]), "String content was not emitted before the closing quote.");
    parser.Feed(Encoding.UTF8.GetBytes("lo\"}"));
    parser.Complete();
    Require(handler.Events.SequenceEqual(["field:text", "chunk:text:h", "chunk:text:e", "chunk:text:l", "chunk:text:l", "chunk:text:o", "value:text:\"hello\"", "complete"]), $"String callback ordering was incorrect: {string.Join(", ", handler.Events)}");
}

static void EscapedCharacters()
{
    var handler = Parse("{\"text\":\"a\\\"b\\\\c\"}", [1, 5, 4, 3, 8]);
    Require(handler.StringContent == "a\"b\\c", "Escaped quote or backslash was decoded incorrectly.");
    Require(handler.FieldValues["text"] == "a\"b\\c", "Completed escaped value was incorrect.");
}

static void UnicodeSurrogatePair()
{
    var handler = Parse("{\"text\":\"\\uD83D\\uDE80\"}", [12, 1, 2, 3, 4]);
    Require(handler.StringContent == "🚀", "Unicode surrogate pair was not decoded.");
}

static void SplitUtf8()
{
    var payload = Encoding.UTF8.GetBytes("{\"text\":\"€\"}");
    var handler = new RecordingHandler();
    var parser = new StreamingJsonParser(handler);
    foreach (var value in payload)
    {
        parser.Feed([value]);
    }

    parser.Complete();
    Require(handler.StringContent == "€", "Split UTF-8 character was not decoded.");
}

static void NestedOrdering()
{
    var handler = Parse("{\"outer\":{\"inner\":\"x\"},\"tail\":true}", [2, 7, 3, 4, 1, 9]);
    var expected = new[] { "field:outer", "field:inner", "chunk:inner:x", "value:inner:\"x\"", "value:outer:{\"inner\":\"x\"}", "field:tail", "value:tail:true", "complete" };
    Require(handler.Events.SequenceEqual(expected), $"Nested event order was incorrect: {string.Join(", ", handler.Events)}");
}

static void ArrayItems()
{
    var handler = Parse("{\"items\":[\"a\",2,{\"ok\":false}]}", [4, 2, 1, 6]);
    var expected = new[] { "item:\"a\"", "item:2", "item:{\"ok\":false}" };
    Require(handler.Events.Where(value => value.StartsWith("item:", StringComparison.Ordinal)).SequenceEqual(expected), $"Completed array item events were incorrect: {string.Join(", ", handler.Events)}");
}

static void MultiLevelNestedArrayOrdering()
{
    var handler = Parse("{\"items\":[[\"a\"],[\"b\",[\"c\"]]]}", [3, 1, 5, 2, 4]);
    var expected = new[]
    {
        "field:items",
        "chunk::a", "item:\"a\"", "item:[\"a\"]",
        "chunk::b", "item:\"b\"", "chunk::c", "item:\"c\"", "item:[\"c\"]", "item:[\"b\",[\"c\"]]",
        "value:items:[[\"a\"],[\"b\",[\"c\"]]]",
        "complete"
    };
    Require(handler.Events.SequenceEqual(expected), $"Multi-level nested-array event order was incorrect: {string.Join(", ", handler.Events)}");
}

static void OneByteChunks()
{
    var handler = Parse("{\"a\":null,\"b\":[1,2],\"c\":\"z\"}", [1]);
    Require(handler.Errors.Count == 0 && handler.Events.Last() == "complete", "One-byte chunk parsing did not complete.");
}

static void MalformedFinalInput()
{
    var handler = new RecordingHandler();
    var parser = new StreamingJsonParser(handler);
    parser.Feed(Encoding.UTF8.GetBytes("{\"text\":\"unterminated"));
    parser.Complete();
    Require(parser.State == StreamingJsonParserState.Error, "Malformed final input did not enter error state.");
    Require(handler.Errors.Count == 1, "Malformed final input did not emit exactly one error.");
}

static void IncompleteUtf8FinalInput()
{
    RequireMalformedFinalInput(
        Encoding.UTF8.GetBytes("{\"text\":\""),
        [0xE2],
        "Incomplete UTF-8 final input was accepted.");
}

static void IncompleteUnicodeEscapeFinalInput()
{
    RequireMalformedFinalInput(
        Encoding.UTF8.GetBytes("{\"text\":\"\\u"),
        Encoding.UTF8.GetBytes("12"),
        "Incomplete Unicode escape final input was accepted.");
}

static void UnpairedSurrogateFinalInput()
{
    RequireMalformedFinalInput(
        Encoding.UTF8.GetBytes("{\"text\":\"\\uD8"),
        Encoding.UTF8.GetBytes("3D\"}"),
        "Unpaired surrogate final input was accepted.");
}

static void RequireMalformedFinalInput(ReadOnlySpan<byte> firstChunk, ReadOnlySpan<byte> secondChunk, string message)
{
    var handler = new RecordingHandler();
    var parser = new StreamingJsonParser(handler);
    parser.Feed(firstChunk);
    parser.Feed(secondChunk);
    parser.Complete();
    Require(parser.State == StreamingJsonParserState.Error, message);
    Require(handler.Errors.Count == 1, $"{message} Expected exactly one error, received {handler.Errors.Count}.");
}

static RecordingHandler Parse(string json, int[] chunkSizes)
{
    var handler = new RecordingHandler();
    var parser = new StreamingJsonParser(handler);
    var bytes = Encoding.UTF8.GetBytes(json);
    var offset = 0;
    var sizeIndex = 0;
    while (offset < bytes.Length)
    {
        var length = Math.Min(chunkSizes[sizeIndex++ % chunkSizes.Length], bytes.Length - offset);
        parser.Feed(bytes.AsSpan(offset, length));
        offset += length;
    }

    parser.Complete();
    Require(parser.State == StreamingJsonParserState.Complete, handler.Errors.FirstOrDefault() ?? "Parser did not complete.");
    return handler;
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

sealed class RecordingHandler : IStreamingJsonHandler
{
    public List<string> Events { get; } = [];
    public List<string> Errors { get; } = [];
    public Dictionary<string, string> FieldValues { get; } = [];
    public string StringContent { get; private set; } = string.Empty;

    public void OnFieldStart(string name) => Events.Add($"field:{name}");

    public void OnStringChunk(JsonStringChunk chunk)
    {
        StringContent += chunk.Value;
        Events.Add($"chunk:{chunk.FieldName}:{chunk.Value}");
    }

    public void OnFieldValue(JsonFieldValue field)
    {
        var value = field.Value.GetRawText();
        FieldValues[field.Name] = field.Value.ValueKind == System.Text.Json.JsonValueKind.String ? field.Value.GetString()! : value;
        Events.Add($"value:{field.Name}:{value}");
    }

    public void OnArrayItem(JsonArrayItem item) => Events.Add($"item:{item.Value.GetRawText()}");

    public void OnDocumentComplete() => Events.Add("complete");

    public void OnError(StreamingJsonError error) => Errors.Add(error.Message);
}
