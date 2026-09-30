#:sdk Microsoft.NET.Sdk
#:package Microsoft.Agents.AI.OpenAI@1.22.0
#:project ../../utilities/DocumentIngestion/DocumentIngestion.csproj
#:project ../../utilities/MinSearch/MinSearch.csproj
#:project ../../utilities/RagSupport/RagSupport.csproj
#:project ../../utilities/StreamingJson/StreamingJson.csproj

// Lesson guide: this runnable script keeps the package directives, environment configuration, and framework agent boundary visible. The local records/classes describe input and output contracts; AsAIAgent constructs the agent and RunAsync or RunStreamingAsync is the deliberate model boundary. Offline fixtures, where present, exercise deterministic local behavior without live model calls. The lesson is intentionally scoped to its named concept rather than production retries, semantic embeddings, or transport policy.
// This variation adds UTF-8 Feed/Complete parsing, ordered callbacks, nested captures, and incremental rendering; its typed RAG helper is deliberately defined but not invoked.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentIngestion;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MinSearch;
using OpenAI;
using OpenAI.Responses;
using RagSupport;
using StreamingJson;

#pragma warning disable OPENAI001 // The OpenAI Responses client is marked experimental by the OpenAI SDK.

if (args.SequenceEqual(["--offline-fixture"]))
{
    RunOfflineFixture();
    return;
}

LoadRepositoryEnvironment();
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY is not configured in the repository .env file.");
var model = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL_NAME") ?? "gpt-4o-mini";
OpenAIClient client = new(apiKey);

// Text streaming is still useful when a schema is not required.
AIAgent textAgent = client.GetResponsesClient().AsAIAgent(
    model: model,
    name: "StoryTextStreamer",
    instructions: "Tell concise bad-time stories.");
await StreamTextAsync(textAgent, "Tell a bad-time story about a unicorn.");

// The article stream uses JSON-schema output. Each JSON delta is rendered incrementally below.
AIAgent articleAgent = client.GetResponsesClient().AsAIAgent(
    model: model,
    name: "ArticleStreamer",
    instructions: "Tell a concise bad-time story using the requested article schema.");
ArticleResponse article = await StreamArticleAsync(articleAgent, "Tell a bad-time story about a unicorn.", Console.Out);
Console.WriteLine($"\nCompleted article: {article.Title} ({article.Sections.Count} sections).\n");

// The RAG stream is deliberately defined, not invoked: the source notebook defines this final scenario.
// Its helper functions compose document ingestion, character chunking, lexical retrieval,
// deterministic context, and a typed stream without adding an execution path to this lesson.
Console.WriteLine("The typed RAG streaming scenario is defined for the lesson but is not invoked here.");

#pragma warning restore OPENAI001

static async Task StreamTextAsync(AIAgent agent, string prompt)
{
    Console.WriteLine("Text stream:");
    await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(prompt))
    {
        Console.Write(update.Text);
    }

    Console.WriteLine("\n");
}

static async Task<ArticleResponse> StreamArticleAsync(AIAgent agent, string prompt, TextWriter output)
{
    var handler = new ArticleResponseHandler(output);
    var parser = new StreamingJsonParser(handler);
    var json = new StringBuilder();
    var options = new AgentRunOptions
    {
        ResponseFormat = ChatResponseFormat.ForJsonSchema<ArticleResponse>()
    };

    await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(prompt, options: options))
    {
        string delta = update.Text ?? string.Empty;
        json.Append(delta);
        parser.Feed(Encoding.UTF8.GetBytes(delta));
    }

    parser.Complete();
    if (parser.State != StreamingJsonParserState.Complete)
    {
        throw new InvalidDataException(parser.Error?.Message ?? "The article JSON stream did not complete.");
    }

    return JsonSerializer.Deserialize(json.ToString(), StructuredRagJsonContext.Default.ArticleResponse)
        ?? throw new JsonException("The article JSON did not match ArticleResponse.");
}

#pragma warning disable CS8321 // The final notebook scenario is intentionally defined without execution.
static async Task<AnswerResponse> StreamRagAsync(
    AIAgent agent,
    IMinSearchIndex index,
    string question,
    TextWriter output)
{
    MinSearchResponse matches = await index.SearchAsync(new MinSearchQuery(question, resultLimit: 5));
    RagFormattedContext formatted = RagContextFormatter.Format(
        question,
        matches.Hits.Select(ToRetrievalRecord),
        "<QUESTION>\n{question}\n</QUESTION>\n\n<CONTEXT>\n{context}\n</CONTEXT>");
    var handler = new RagResponseHandler(output);
    var parser = new StreamingJsonParser(handler);
    var json = new StringBuilder();
    var options = new AgentRunOptions { ResponseFormat = ChatResponseFormat.ForJsonSchema<AnswerResponse>() };

    await foreach (AgentResponseUpdate update in agent.RunStreamingAsync(formatted.RenderedContext, options: options))
    {
        string delta = update.Text ?? string.Empty;
        json.Append(delta);
        parser.Feed(Encoding.UTF8.GetBytes(delta));
    }

    parser.Complete();
    if (parser.State != StreamingJsonParserState.Complete)
    {
        throw new InvalidDataException(parser.Error?.Message ?? "The RAG JSON stream did not complete.");
    }

    return JsonSerializer.Deserialize(json.ToString(), StructuredRagJsonContext.Default.AnswerResponse)
        ?? throw new JsonException("The RAG JSON did not match AnswerResponse.");
}

static async Task<IMinSearchIndex> BuildRagIndexAsync(IDocumentSource source)
{
    IReadOnlyList<IngestedDocument> documents = (await source.ReadAsync()).Select(DocumentMapper.Map).ToList();
    var index = new InMemoryMinSearchIndex(new MinSearchIndexOptions(
        textFields: ["title", "description", "content"],
        keywordFields: ["filename"]));
    var chunks = documents.SelectMany(document => CharacterWindowChunker.Chunk(
        document.Filename,
        document.Content,
        new CharacterWindowChunkingOptions(Size: 3000, Step: 1500),
        document.Title));

    await index.FitAsync(chunks.Select(chunk => new MinSearchDocument(
        chunk.ChunkId,
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = chunk.Provenance.Label ?? string.Empty,
            ["description"] = chunk.SourceIdentifier,
            ["content"] = chunk.Text,
            ["filename"] = chunk.SourceIdentifier
        })));
    return index;
}

static RagRetrievalRecord ToRetrievalRecord(MinSearchHit hit)
{
    IReadOnlyDictionary<string, string> fields = hit.Document.Fields;
    string filename = fields["filename"];
    return new RagRetrievalRecord(hit.Document.Id, filename, 0, fields["content"], new ChunkProvenance(filename, 0, fields["content"].Length, fields["title"]));
}
#pragma warning restore CS8321

static void RunOfflineFixture()
{
    const string articleJson = "{\"title\":\"Fixture article\",\"subtitle\":\"Rendered safely\",\"sections\":[{\"header\":\"First\",\"text\":\"Early text\"}]}";
    const string ragJson = "{\"foundAnswer\":true,\"answer\":{\"answer\":\"Early answer\",\"confidence\":0.9,\"confidenceExplanation\":\"Fixture\",\"answerType\":\"how-to\",\"followupQuestions\":[\"What next?\"]},\"answerNotFound\":null}";
    var output = new StringWriter();
    var articleParser = new StreamingJsonParser(new ArticleResponseHandler(output));
    foreach (byte value in Encoding.UTF8.GetBytes(articleJson))
    {
        articleParser.Feed([value]);
    }
    articleParser.Complete();

    var ragParser = new StreamingJsonParser(new RagResponseHandler(output));
    foreach (byte value in Encoding.UTF8.GetBytes(ragJson))
    {
        ragParser.Feed([value]);
    }
    ragParser.Complete();

    ArticleResponse? article = JsonSerializer.Deserialize(articleJson, StructuredRagJsonContext.Default.ArticleResponse);
    AnswerResponse? answer = JsonSerializer.Deserialize(ragJson, StructuredRagJsonContext.Default.AnswerResponse);

    string rendered = output.ToString();
    if (articleParser.State != StreamingJsonParserState.Complete || ragParser.State != StreamingJsonParserState.Complete ||
        article?.Title != "Fixture article" || answer?.Answer?.Answer != "Early answer" ||
        !rendered.Contains("# Fixture article", StringComparison.Ordinal) ||
        !rendered.Contains("Early text", StringComparison.Ordinal) ||
        !rendered.Contains("Early answer", StringComparison.Ordinal) ||
        !rendered.Contains("Follow-up: What next?", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("Offline fixture did not observe early string rendering and completed field/array-item handling.");
    }

    Console.WriteLine("SUCCESS: offline StreamingJson article and RAG handler fixture passed.");
}

static void LoadRepositoryEnvironment()
{
    string environmentFile = Path.Combine(FindRepositoryRoot(), ".env");
    if (!File.Exists(environmentFile))
    {
        return;
    }

    foreach (string line in File.ReadLines(environmentFile))
    {
        string trimmed = line.Trim();
        int separator = trimmed.IndexOf('=');
        if (trimmed.Length == 0 || trimmed.StartsWith('#') || separator <= 0)
        {
            continue;
        }

        string key = trimmed[..separator].Trim();
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, trimmed[(separator + 1)..].Trim().Trim('"', '\''));
        }
    }
}

static string FindRepositoryRoot()
{
    for (DirectoryInfo? directory = new(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
    {
        string gitMarker = Path.Combine(directory.FullName, ".git");
        if (Directory.Exists(gitMarker) || File.Exists(gitMarker))
        {
            return directory.FullName;
        }
    }

    throw new DirectoryNotFoundException("Could not find the repository root needed to load .env.");
}

// ArticleResponse is the lesson's explicit data or orchestration boundary; keeping it local makes the phase being taught visible.
public sealed class ArticleResponse
{
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("title")] public required string Title { get; init; }
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("subtitle")] public required string Subtitle { get; init; }
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("sections")] public required IReadOnlyList<ArticleSection> Sections { get; init; }
}

// ArticleSection is the lesson's explicit data or orchestration boundary; keeping it local makes the phase being taught visible.
public sealed class ArticleSection
{
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("header")] public required string Header { get; init; }
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("text")] public required string Text { get; init; }
}

// RAGResponse is the lesson's explicit data or orchestration boundary; keeping it local makes the phase being taught visible.
public sealed class RAGResponse
{
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("answer")] public required string Answer { get; init; }
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("confidence")] public required double Confidence { get; init; }
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("confidenceExplanation")] public required string ConfidenceExplanation { get; init; }
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("answerType")] public required string AnswerType { get; init; }
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("followupQuestions")] public required IReadOnlyList<string> FollowupQuestions { get; init; }
}

// AnswerNotFound is the lesson's explicit data or orchestration boundary; keeping it local makes the phase being taught visible.
public sealed class AnswerNotFound
{
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("explanation")] public required string Explanation { get; init; }
}

// AnswerResponse is the lesson's explicit data or orchestration boundary; keeping it local makes the phase being taught visible.
public sealed class AnswerResponse
{
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("foundAnswer")] public required bool FoundAnswer { get; init; }
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("answer")] public RAGResponse? Answer { get; init; }
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("answerNotFound")] public AnswerNotFound? AnswerNotFound { get; init; }
}

[JsonSerializable(typeof(ArticleResponse))]
[JsonSerializable(typeof(AnswerResponse))]
internal sealed partial class StructuredRagJsonContext : JsonSerializerContext;

// ArticleResponseHandler is the lesson's explicit data or orchestration boundary; keeping it local makes the phase being taught visible.
public sealed class ArticleResponseHandler(TextWriter output) : IStreamingJsonHandler
{
    public void OnFieldStart(string name) { }
    public void OnStringChunk(JsonStringChunk chunk)
    {
        if (chunk.FieldName == "text") output.Write(chunk.Value);
    }
    public void OnFieldValue(JsonFieldValue field)
    {
        if (field.Name == "title") output.WriteLine($"# {field.Value.GetString()}");
        if (field.Name == "subtitle") output.WriteLine(field.Value.GetString());
        if (field.Name == "header") output.WriteLine($"\n## {field.Value.GetString()}\n");
    }
    public void OnArrayItem(JsonArrayItem item) { }
    public void OnDocumentComplete() { }
    public void OnError(StreamingJsonError error) => throw new InvalidDataException(error.Message);
}

// RagResponseHandler is the lesson's explicit data or orchestration boundary; keeping it local makes the phase being taught visible.
public sealed class RagResponseHandler(TextWriter output) : IStreamingJsonHandler
{
    public void OnFieldStart(string name) { }
    public void OnStringChunk(JsonStringChunk chunk)
    {
        if (chunk.FieldName == "answer") output.Write(chunk.Value);
    }
    public void OnFieldValue(JsonFieldValue field)
    {
        if (field.Name == "answerType") output.WriteLine($"\nAnswer type: {field.Value.GetString()}");
    }
    public void OnArrayItem(JsonArrayItem item)
    {
        if (item.Value.ValueKind == JsonValueKind.String) output.WriteLine($"Follow-up: {item.Value.GetString()}");
    }
    public void OnDocumentComplete() { }
    public void OnError(StreamingJsonError error) => throw new InvalidDataException(error.Message);
}
