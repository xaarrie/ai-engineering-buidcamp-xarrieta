#:sdk Microsoft.NET.Sdk
#:package Microsoft.Agents.AI.OpenAI@1.22.0
#:project ../../dotnet/utilities/DocumentIngestion/DocumentIngestion.csproj
#:project ../../dotnet/utilities/MinSearch/MinSearch.csproj
#:project ../../dotnet/utilities/RagSupport/RagSupport.csproj

// Lesson guide: the manual baseline makes retrieval and prompt construction explicit.
// The agentic path gives the same deterministic retrieval boundary to ChatClientAgent
// as an AIFunction, so Microsoft Agent Framework owns tool calls and the final answer.
// The --verify path never loads .env and never creates a model/API client.

using System.Text.Json;
using System.Text.Json.Serialization;
using DocumentIngestion;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using MinSearch;
using OpenAI;
using RagSupport;

if (args is ["--verify"])
{
    await VerifyOfflineAsync();
    return;
}

if (!TryParseArguments(args, out LessonOptions options, out string? error))
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
    return;
}

LoadRepositoryEnvironment();
string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY is not configured in the repository .env file.");
string model = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL_NAME") ?? "gpt-4o-mini";

using var httpClient = options.LocalDirectory is null ? new HttpClient() : null;
IDocumentSource source = options.LocalDirectory is null
    ? new GitHubZipDocumentSource(httpClient!, options.Owner, options.Repository, options.Branch)
    : new LocalDirectoryDocumentSource(options.LocalDirectory);
using InMemoryMinSearchIndex index = await RagLesson.BuildIndexAsync(source);

Console.WriteLine("Manual RAG baseline: retrieve first, then render the prompt explicitly.");
RagFormattedContext baselineContext = await RagLesson.BuildContextAsync(index, options.Question);
Console.WriteLine(baselineContext.RenderedContext);

OpenAIClient client = new(apiKey);
ChatClientAgent baselineAgent = new(
    chatClient: client.GetChatClient(model).AsIChatClient(),
    name: "ManualRagAnswerer",
    instructions: "Answer only from the supplied documentation context. If it is missing, say so and include no invented citations.");
AgentResponse<AnswerContract> baseline = await baselineAgent.RunAsync<AnswerContract>(baselineContext.RenderedContext);
Console.WriteLine($"Manual baseline answer:\n{baseline.Result.Answer}");
PrintUsageNote("manual baseline");

AIFunction retrievalTool = AIFunctionFactory.Create(
    (string query, int limit) => RagLesson.RetrieveContext(index, query, limit),
    name: "retrieve_documentation",
    description: "Search the documentation index and return deterministic structured context with source citations. Call this before answering documentation questions; report when no matching context is found.");

// ChatClientAgent is model-bound and Agent Framework owns the tool-calling loop.
ChatClientAgent agent = new(
    chatClient: client.GetChatClient(model).AsIChatClient(),
    name: "AgenticDocumentationRag",
    instructions: "You are a documentation assistant. Use retrieve_documentation before answering. Use only returned context, cite its sources, and say clearly when no result is found.",
    tools: [retrievalTool]);
AgentResponse<AnswerContract> agentic = await agent.RunAsync<AnswerContract>(options.Question);
Console.WriteLine($"Agentic RAG answer:\n{agentic.Result.Answer}");
Console.WriteLine("Sources:");
foreach (AnswerCitation citation in agentic.Result.Citations)
{
    Console.WriteLine($"- {citation.SourceIdentifier} ({citation.ChunkId})");
}
PrintUsageNote("agentic RAG");

static void PrintUsageNote(string path)
{
    Console.WriteLine($"Token-usage comparison ({path}) is live-path-only; usage metadata may be unavailable from the selected client/response and is not inferred here.");
}

static async Task VerifyOfflineAsync()
{
    string directory = Path.Combine(Path.GetTempPath(), $"agentic-rag-fixture-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(directory, "guides"));
    try
    {
        await File.WriteAllTextAsync(
            Path.Combine(directory, "guides", "dashboard.md"),
            "---\ntitle: Dashboards\ndescription: Dashboard guide\n---\nCreate a dashboard from the Dashboards page, then add panels to it.");
        await File.WriteAllTextAsync(Path.Combine(directory, "ignored.txt"), "This is not a document source.");

        using InMemoryMinSearchIndex index = await RagLesson.BuildIndexAsync(new LocalDirectoryDocumentSource(directory));
        string result = RagLesson.RetrieveContext(index, "How do I create a dashboard?", 3);
        if (!result.Contains("guides/dashboard.md", StringComparison.Ordinal) ||
            result.Contains("ignored.txt", StringComparison.Ordinal) ||
            !result.Contains("Create a dashboard", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Retrieval success fixture failed.");
        }

        string noResult = RagLesson.RetrieveContext(index, "quantum banana", 3);
        if (!noResult.Contains("\"found\":false", StringComparison.Ordinal) ||
            !noResult.Contains("No matching documentation was found", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Retrieval no-result fixture failed.");
        }

        AssertThrows<ArgumentException>(() => RagLesson.RetrieveContext(index, "  ", 3));
        AssertThrows<ArgumentOutOfRangeException>(() => RagLesson.RetrieveContext(index, "dashboard", 0));
        AssertThrows<ArgumentOutOfRangeException>(() => RagLesson.RetrieveContext(index, "dashboard", 6));

        RagFormattedContext first = await RagLesson.BuildContextAsync(index, "How do I create a dashboard?");
        RagFormattedContext second = await RagLesson.BuildContextAsync(index, "How do I create a dashboard?");
        if (!string.Equals(first.ContextJson, second.ContextJson, StringComparison.Ordinal) ||
            !string.Equals(first.RenderedContext, second.RenderedContext, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Context rendering was not deterministic.");
        }

        AnswerContract contract = new(
            "Create it from the Dashboards page.",
            true,
            "how-to",
            [new AnswerCitation("guides/dashboard.md", "chunk-1")]);
        string serialized = JsonSerializer.Serialize(contract, LessonJsonContext.Default.AnswerContract);
        AnswerContract? roundTrip = JsonSerializer.Deserialize(serialized, LessonJsonContext.Default.AnswerContract);
        if (roundTrip is null || !roundTrip.FoundAnswer || roundTrip.Citations.Count != 1 ||
            !string.Equals(roundTrip.Citations[0].SourceIdentifier, "guides/dashboard.md", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Typed-contract serialization fixture failed.");
        }

        Console.WriteLine("SUCCESS: offline retrieval success/no-result/validation, deterministic context, and typed-contract serialization passed.");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void AssertThrows<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static bool TryParseArguments(string[] args, out LessonOptions options, out string? error)
{
    string owner = "evidentlyai", repository = "docs", branch = "main";
    string question = "How do I create a dashboard in Evidently?";
    string? localDirectory = null;

    for (int index = 0; index < args.Length; index += 2)
    {
        if (index + 1 >= args.Length || args[index] is not ("--local" or "--owner" or "--repo" or "--branch" or "--question"))
        {
            options = default!;
            error = "Usage: [--local <directory>] [--owner <owner>] [--repo <repository>] [--branch <branch>] [--question <text>]";
            return false;
        }

        switch (args[index])
        {
            case "--local": localDirectory = Path.GetFullPath(args[index + 1]); break;
            case "--owner": owner = args[index + 1]; break;
            case "--repo": repository = args[index + 1]; break;
            case "--branch": branch = args[index + 1]; break;
            case "--question": question = args[index + 1]; break;
        }
    }

    if (localDirectory is not null && !Directory.Exists(localDirectory))
    {
        options = default!;
        error = $"The local document directory does not exist: {localDirectory}";
        return false;
    }

    options = new LessonOptions(owner, repository, branch, localDirectory, question);
    error = null;
    return true;
}

static void LoadRepositoryEnvironment()
{
    string environmentFile = Path.Combine(FindRepositoryRoot(), ".env");
    if (!File.Exists(environmentFile)) return;
    foreach (string line in File.ReadLines(environmentFile))
    {
        string trimmed = line.Trim();
        int separator = trimmed.IndexOf('=');
        if (trimmed.Length == 0 || trimmed.StartsWith('#') || separator <= 0) continue;
        string key = trimmed[..separator].Trim();
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, trimmed[(separator + 1)..].Trim().Trim('"', '\''));
    }
}

static string FindRepositoryRoot()
{
    for (DirectoryInfo? directory = new(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
    {
        string gitMarker = Path.Combine(directory.FullName, ".git");
        if (Directory.Exists(gitMarker) || File.Exists(gitMarker)) return directory.FullName;
    }
    throw new DirectoryNotFoundException("Could not find the repository root needed to load .env.");
}

sealed record LessonOptions(string Owner, string Repository, string Branch, string? LocalDirectory, string Question);

public sealed record AnswerCitation(
    [property: JsonPropertyName("sourceIdentifier")] string SourceIdentifier,
    [property: JsonPropertyName("chunkId")] string ChunkId);

public sealed record AnswerContract(
    [property: JsonPropertyName("answer")] string Answer,
    [property: JsonPropertyName("foundAnswer")] bool FoundAnswer,
    [property: JsonPropertyName("answerType")] string AnswerType,
    [property: JsonPropertyName("citations")] IReadOnlyList<AnswerCitation> Citations);

public sealed record RetrievalToolResponse(
    [property: JsonPropertyName("found")] bool Found,
    [property: JsonPropertyName("message")] string Message,
    [property: JsonPropertyName("citations")] IReadOnlyList<AnswerCitation> Citations,
    [property: JsonPropertyName("context")] string Context);

[JsonSerializable(typeof(AnswerContract))]
[JsonSerializable(typeof(RetrievalToolResponse))]
internal partial class LessonJsonContext : JsonSerializerContext;

static class RagLesson
{
    private const string PromptTemplate = "<QUESTION>\n{question}\n</QUESTION>\n\n<CONTEXT>\n{context}\n</CONTEXT>";

    public static async Task<InMemoryMinSearchIndex> BuildIndexAsync(IDocumentSource source)
    {
        IReadOnlyList<IngestedDocument> documents = (await source.ReadAsync()).Select(DocumentMapper.Map).ToList();
        var index = new InMemoryMinSearchIndex(new MinSearchIndexOptions(["title", "description", "content"], ["filename"]));
        await index.FitAsync(documents.SelectMany(document =>
            CharacterWindowChunker.Chunk(document.Filename, document.Content, new CharacterWindowChunkingOptions(3000, 1500), document.Title)
                .Select(chunk => new MinSearchDocument(chunk.ChunkId, new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["title"] = document.Title,
                    ["description"] = document.Description,
                    ["content"] = chunk.Text,
                    ["filename"] = document.Filename
                }))));
        return index;
    }

    public static async Task<RagFormattedContext> BuildContextAsync(IMinSearchIndex index, string question)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        MinSearchResponse search = await index.SearchAsync(new MinSearchQuery(question.Trim(), resultLimit: 5));
        return RagContextFormatter.Format(question.Trim(), ToRecords(search), PromptTemplate);
    }

    public static string RetrieveContext(IMinSearchIndex index, string query, int limit = 5)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(query);
        if (limit is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 5.");

        MinSearchResponse search = index.SearchAsync(new MinSearchQuery(query.Trim(), resultLimit: limit)).GetAwaiter().GetResult();
        RagRetrievalRecord[] records = ToRecords(search).ToArray();
        RagFormattedContext formatted = RagContextFormatter.Format(query.Trim(), records, PromptTemplate);
        var response = new RetrievalToolResponse(
            records.Length > 0,
            records.Length > 0 ? "Retrieved documentation context." : "No matching documentation was found.",
            records.Select(record => new AnswerCitation(record.SourceIdentifier, record.ChunkId)).ToArray(),
            formatted.ContextJson);
        return JsonSerializer.Serialize(response, LessonJsonContext.Default.RetrievalToolResponse);
    }

    private static IEnumerable<RagRetrievalRecord> ToRecords(MinSearchResponse search) => search.Hits.Select(hit =>
    {
        IReadOnlyDictionary<string, string> fields = hit.Document.Fields;
        string content = fields["content"], filename = fields["filename"];
        return new RagRetrievalRecord(hit.Document.Id, filename, 0, content,
            new ChunkProvenance(filename, 0, content.Length, fields["title"]));
    });

}
