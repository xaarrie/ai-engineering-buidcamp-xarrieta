#:sdk Microsoft.NET.Sdk
#:package Microsoft.Agents.AI.OpenAI@1.22.0
#:project ../../utilities/DocumentIngestion/DocumentIngestion.csproj
#:project ../../utilities/MinSearch/MinSearch.csproj
#:project ../../utilities/RagSupport/RagSupport.csproj

// Lesson guide: this runnable script keeps the package directives, environment configuration, and framework agent boundary visible. The local records/classes describe input and output contracts; AsAIAgent constructs the agent and RunAsync or RunStreamingAsync is the deliberate model boundary. Offline fixtures, where present, exercise deterministic local behavior without live model calls. The lesson is intentionally scoped to its named concept rather than production retries, semantic embeddings, or transport policy.
// rag_v5 composes source contracts, frontmatter mapping, character (not token) windows, TF-IDF retrieval, provenance, deterministic escaped context, and a typed answer.

using System.Text.Json.Serialization;
using DocumentIngestion;
using Microsoft.Agents.AI;
using MinSearch;
using OpenAI;
using OpenAI.Responses;
using RagSupport;

#pragma warning disable OPENAI001 // The Responses client is experimental in the OpenAI SDK.

if (args.SequenceEqual(["--offline-fixture"]))
{
    await RunOfflineFixtureAsync();
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

using var client = options.LocalDirectory is null ? new HttpClient() : null;
IDocumentSource source = options.LocalDirectory is null
    ? new GitHubZipDocumentSource(client!, options.Owner, options.Repository, options.Branch)
    : new LocalDirectoryDocumentSource(options.LocalDirectory);
using var index = await Rag.BuildIndexAsync(source);
OpenAIClient openAI = new(apiKey);
AIAgent agent = openAI.GetResponsesClient().AsAIAgent(
    model: model,
    name: "DocumentationRag",
    instructions: "Answer only from the documentation context. If it does not contain the answer, say so.");
var rag = new Rag(index, agent);
RagResponse answer = await rag.AskAsync(options.Question);
Console.WriteLine(answer.FoundAnswer ? answer.Answer : "No answer was found in the retrieved context.");
Console.WriteLine($"Confidence: {answer.Confidence:F2}; type: {answer.AnswerType}");
Console.WriteLine($"Follow-up questions: {string.Join("; ", answer.FollowupQuestions)}");

#pragma warning restore OPENAI001

static async Task RunOfflineFixtureAsync()
{
    string directory = Path.Combine(Path.GetTempPath(), $"rag-v5-fixture-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(directory, "guides"));
    try
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "guides", "judge.md"), "---\ntitle: LLM judge\ndescription: Evaluation guide\n---\nUse an LLM as a judge to evaluate generated answers.");
        await File.WriteAllTextAsync(Path.Combine(directory, "ignored.txt"), "not markdown");
        using InMemoryMinSearchIndex index = await Rag.BuildIndexAsync(new LocalDirectoryDocumentSource(directory));
        RagFormattedContext context = await Rag.BuildContextAsync(index, "How do I use an LLM as a judge?");
        if (!context.ContextJson.Contains("guides/judge.md", StringComparison.Ordinal) ||
            context.ContextJson.Contains("ignored.txt", StringComparison.Ordinal) ||
            !context.RenderedContext.Contains("<QUESTION>", StringComparison.Ordinal))
            throw new InvalidOperationException("Offline ingestion, chunking, retrieval, or context fixture failed.");
        Console.WriteLine("SUCCESS: offline ingestion, chunking, retrieval, and context fixture passed.");
    }
    finally { Directory.Delete(directory, recursive: true); }
}

static bool TryParseArguments(string[] args, out LessonOptions options, out string? error)
{
    string owner = "evidentlyai", repository = "docs", branch = "main", question = "How do I use an LLM as a judge?";
    string? local = null;
    for (int index = 0; index < args.Length; index += 2)
    {
        if (index + 1 >= args.Length || args[index] is not ("--local" or "--owner" or "--repo" or "--branch" or "--question"))
        {
            options = default!; error = "Usage: [--local <directory>] [--owner <owner>] [--repo <repository>] [--branch <branch>] [--question <text>]"; return false;
        }
        switch (args[index])
        {
            case "--local": local = Path.GetFullPath(args[index + 1]); break;
            case "--owner": owner = args[index + 1]; break;
            case "--repo": repository = args[index + 1]; break;
            case "--branch": branch = args[index + 1]; break;
            case "--question": question = args[index + 1]; break;
        }
    }
    if (local is not null && !Directory.Exists(local)) { options = default!; error = $"Local directory does not exist: {local}"; return false; }
    options = new LessonOptions(owner, repository, branch, local, question); error = null; return true;
}

static void LoadRepositoryEnvironment()
{
    string file = Path.Combine(FindRepositoryRoot(), ".env");
    if (!File.Exists(file)) return;
    foreach (string line in File.ReadLines(file))
    {
        string text = line.Trim(); int equals = text.IndexOf('=');
        if (text.Length > 0 && !text.StartsWith('#') && equals > 0 && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(text[..equals].Trim())))
            Environment.SetEnvironmentVariable(text[..equals].Trim(), text[(equals + 1)..].Trim().Trim('"', '\''));
    }
}
static string FindRepositoryRoot()
{
    for (DirectoryInfo? directory = new(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
        if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
    throw new DirectoryNotFoundException("Could not find the repository root.");
}

// Owner, Repository, and Branch identify the remote archive; LocalDirectory switches to a local source when set; Question is sent to retrieval.
sealed record LessonOptions(string Owner, string Repository, string Branch, string? LocalDirectory, string Question);

// This focused abstraction mirrors rag.py: retrieve, build a prompt, then ask the typed agent.
public sealed class Rag(IMinSearchIndex index, AIAgent? agent = null)
{
    private const string PromptTemplate = "<QUESTION>\n{question}\n</QUESTION>\n\n<CONTEXT>\n{context}\n</CONTEXT>";

    public async Task<RagResponse> AskAsync(string question)
    {
        if (agent is null) throw new InvalidOperationException("An agent is required to generate an answer.");
        RagFormattedContext context = await BuildContextAsync(index, question);
        AgentResponse<RagResponse> response = await agent.RunAsync<RagResponse>(context.RenderedContext);
        return response.Result;
    }

    public static async Task<InMemoryMinSearchIndex> BuildIndexAsync(IDocumentSource source)
    {
        IReadOnlyList<IngestedDocument> documents = (await source.ReadAsync()).Select(DocumentMapper.Map).ToList();
        var index = new InMemoryMinSearchIndex(new MinSearchIndexOptions(["title", "description", "content"], ["filename"]));
        await index.FitAsync(documents.SelectMany(document => CharacterWindowChunker.Chunk(
            document.Filename, document.Content, new CharacterWindowChunkingOptions(3000, 1500), document.Title)
            .Select(chunk => new MinSearchDocument(chunk.ChunkId, new Dictionary<string, string>
            {
                ["title"] = document.Title, ["description"] = document.Description,
                ["content"] = chunk.Text, ["filename"] = document.Filename
            }))));
        return index;
    }

    public static async Task<RagFormattedContext> BuildContextAsync(IMinSearchIndex index, string question)
    {
        MinSearchResponse search = await index.SearchAsync(new MinSearchQuery(question, resultLimit: 5));
        return RagContextFormatter.Format(question, search.Hits.Select(hit =>
        {
            var fields = hit.Document.Fields;
            string content = fields["content"], filename = fields["filename"];
            return new RagRetrievalRecord(hit.Document.Id, filename, 0, content,
                new ChunkProvenance(filename, 0, content.Length, fields["title"]));
        }), PromptTemplate);
    }
}

// These properties define the JSON schema returned by the typed retrieval-and-answer contract.
public sealed class RagResponse
{
    // The grounded response text; callers display it only on the FoundAnswer branch.
    [JsonPropertyName("answer")] public required string Answer { get; init; }
    // True means retrieved chunks support the answer; false selects the explicit not-found message.
    [JsonPropertyName("foundAnswer")] public required bool FoundAnswer { get; init; }
    // The model's confidence score, displayed with the answer as a compact evidence signal.
    [JsonPropertyName("confidence")] public required double Confidence { get; init; }
    // Human-readable context for the score, explaining why the model assigned that confidence.
    [JsonPropertyName("confidenceExplanation")] public required string ConfidenceExplanation { get; init; }
    // Describes the answer form so consumers can distinguish the kind of grounded response returned.
    [JsonPropertyName("answerType")] public required string AnswerType { get; init; }
    // Questions to ask next when evidence is incomplete or the original question needs clarification.
    [JsonPropertyName("followupQuestions")] public required IReadOnlyList<string> FollowupQuestions { get; init; }
}
