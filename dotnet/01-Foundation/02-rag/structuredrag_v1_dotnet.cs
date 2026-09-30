#:sdk Microsoft.NET.Sdk
#:package Microsoft.Agents.AI.OpenAI@1.22.0
#:project ../../utilities/DocumentIngestion/DocumentIngestion.csproj
#:project ../../utilities/MinSearch/MinSearch.csproj
#:project ../../utilities/RagSupport/RagSupport.csproj

// Lesson guide: this runnable script keeps the package directives, environment configuration, and framework agent boundary visible. The local records/classes describe input and output contracts; AsAIAgent constructs the agent and RunAsync or RunStreamingAsync is the deliberate model boundary. Offline fixtures, where present, exercise deterministic local behavior without live model calls. The lesson is intentionally scoped to its named concept rather than production retries, semantic embeddings, or transport policy.
// This variation compares plain text, typed output, and a found/not-found union while keeping ingest, chunk, retrieve, and format phases inspectable.

using System.Text.Json.Serialization;
using DocumentIngestion;
using Microsoft.Agents.AI;
using MinSearch;
using OpenAI;
using OpenAI.Responses;
using RagSupport;

#pragma warning disable OPENAI001 // The OpenAI Responses client is marked experimental by the OpenAI SDK.

if (args.SequenceEqual(["--offline-fixture"]))
{
    await RunOfflineFixtureAsync();
    return;
}

if (!TryParseArguments(args, out LessonInput input, out string? error))
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
    return;
}

LoadRepositoryEnvironment();
var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY is not configured in the repository .env file.");
var model = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL_NAME") ?? "gpt-4o-mini";

using var httpClient = input.LocalDirectory is null ? new HttpClient() : null;
IDocumentSource source = input.LocalDirectory is null
    ? new GitHubZipDocumentSource(httpClient!, input.Owner, input.Repository, input.Branch)
    : new LocalDirectoryDocumentSource(input.LocalDirectory);
using InMemoryMinSearchIndex index = await BuildIndexAsync(source);
RagFormattedContext context = await BuildContextAsync(index, input.Question);

Console.WriteLine($"Indexed documentation with 3000-character windows and a 1500-character step.");
Console.WriteLine("Retrieved the lexical top five results and rendered deterministic JSON context.");

OpenAIClient client = new(apiKey);
AIAgent agent = client.GetResponsesClient().AsAIAgent(
    model: model,
    name: "DocumentationRag",
    instructions: "Answer only from the supplied documentation context. If it is missing, say so.");

AgentResponse unstructured = await agent.RunAsync(context.RenderedContext);
Console.WriteLine($"Unstructured RAG response:\n{unstructured.Text}\n");

AgentResponse<RagResponse> basic = await agent.RunAsync<RagResponse>(context.RenderedContext);
Console.WriteLine($"Typed answer found: {basic.Result.FoundAnswer}");

AgentResponse<AnswerResponse> final = await agent.RunAsync<AnswerResponse>(context.RenderedContext);
final.Result.ValidateUnion();
Console.WriteLine(final.Result.FoundAnswer
    ? $"Typed answer: {final.Result.Answer!.Answer}"
    : $"Not found: {final.Result.AnswerNotFound!.Explanation}");

#pragma warning restore OPENAI001

static async Task<InMemoryMinSearchIndex> BuildIndexAsync(IDocumentSource source)
{
    IReadOnlyList<IngestedDocument> documents = (await source.ReadAsync()).Select(DocumentMapper.Map).ToList();
    var index = new InMemoryMinSearchIndex(new MinSearchIndexOptions(
        textFields: ["title", "description", "content"],
        keywordFields: ["filename"]));
    IEnumerable<MinSearchDocument> chunkedDocuments = documents.SelectMany(document =>
        CharacterWindowChunker.Chunk(
            document.Filename,
            document.Content,
            new CharacterWindowChunkingOptions(Size: 3000, Step: 1500),
            document.Title)
        .Select(chunk => ToMinSearchDocument(document, chunk)));
    await index.FitAsync(chunkedDocuments);
    return index;
}

static async Task<RagFormattedContext> BuildContextAsync(IMinSearchIndex index, string question)
{
    MinSearchResponse search = await index.SearchAsync(new MinSearchQuery(question, resultLimit: 5));
    return RagContextFormatter.Format(
        question,
        search.Hits.Select(hit =>
        {
            IReadOnlyDictionary<string, string> fields = hit.Document.Fields;
            string text = fields["content"];
            string filename = fields["filename"];
            return new RagRetrievalRecord(hit.Document.Id, filename, 0, text, new ChunkProvenance(filename, 0, text.Length, fields["title"]));
        }),
        "<QUESTION>\n{question}\n</QUESTION>\n\n<CONTEXT>\n{context}\n</CONTEXT>");
}

static MinSearchDocument ToMinSearchDocument(IngestedDocument document, RagChunk chunk) => new(
    chunk.ChunkId,
    new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["title"] = document.Title,
        ["description"] = document.Description,
        ["content"] = chunk.Text,
        ["filename"] = document.Filename
    });

static async Task RunOfflineFixtureAsync()
{
    string directory = Path.Combine(Path.GetTempPath(), $"structuredrag-fixture-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(directory, "guides"));
    try
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "guides", "judge.md"), "---\ntitle: LLM judge\ndescription: Retrieval fixture\n---\nUse an LLM as a judge to evaluate answers.");
        await File.WriteAllTextAsync(Path.Combine(directory, "ignored.txt"), "This must not be ingested.");
        using InMemoryMinSearchIndex index = await BuildIndexAsync(new LocalDirectoryDocumentSource(directory));
        RagFormattedContext context = await BuildContextAsync(index, "How do I use an LLM as a judge?");
        if (!context.ContextJson.Contains("guides/judge.md", StringComparison.Ordinal) ||
            !context.RenderedContext.Contains("<QUESTION>", StringComparison.Ordinal) ||
            context.ContextJson.Contains("ignored.txt", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The local ingestion, chunking, retrieval, or context fixture did not produce the expected result.");
        }

        Console.WriteLine("SUCCESS: offline ingestion, chunking, retrieval, and context fixture passed.");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static bool TryParseArguments(string[] args, out LessonInput input, out string? error)
{
    string owner = "evidentlyai";
    string repository = "docs";
    string branch = "main";
    string question = "How do I run LLM evaluations?";
    string? localDirectory = null;
    for (int index = 0; index < args.Length; index += 2)
    {
        if (index + 1 >= args.Length || args[index] is not ("--local" or "--owner" or "--repo" or "--branch" or "--question"))
        {
            input = default!;
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
        input = default!;
        error = $"The local document directory does not exist: {localDirectory}";
        return false;
    }

    input = new LessonInput(owner, repository, branch, localDirectory, question);
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

sealed record LessonInput(string Owner, string Repository, string Branch, string? LocalDirectory, string Question);

// RagResponse is the lesson's explicit data or orchestration boundary; keeping it local makes the phase being taught visible.
public sealed class RagResponse
{
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("answer")] public required string Answer { get; init; }
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("foundAnswer")] public required bool FoundAnswer { get; init; }
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
    [JsonPropertyName("answer")] public RagResponse? Answer { get; init; }
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("answerNotFound")] public AnswerNotFound? AnswerNotFound { get; init; }

    public void ValidateUnion()
    {
        if (FoundAnswer != (Answer is not null) || FoundAnswer == (AnswerNotFound is not null))
            throw new InvalidDataException("A response must contain exactly one matching found/not-found branch.");
    }
}
