#:sdk Microsoft.NET.Sdk
#:package Microsoft.Agents.AI.OpenAI@1.22.0
#:project ../../utilities/DocumentIngestion/DocumentIngestion.csproj
#:project ../../utilities/MinSearch/MinSearch.csproj
#:project ../../utilities/RagSupport/RagSupport.csproj

// Lesson guide: this runnable script keeps the package directives, environment configuration, and framework agent boundary visible. The local records/classes describe input and output contracts; AsAIAgent constructs the agent and RunAsync or RunStreamingAsync is the deliberate model boundary. Offline fixtures, where present, exercise deterministic local behavior without live model calls. The lesson is intentionally scoped to its named concept rather than production retries, semantic embeddings, or transport policy.

using System.Text.Json.Serialization;
using DocumentIngestion;
using Microsoft.Agents.AI;
using MinSearch;
using OpenAI;
using OpenAI.Embeddings;
using OpenAI.Responses;
using RagSupport;

#pragma warning disable OPENAI001 // The Responses client is experimental in the OpenAI SDK.

if (args.SequenceEqual(["--offline-fixture"]))
{
    await RunOfflineFixtureAsync();
    return;
}

LoadRepositoryEnvironment();
string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY is not configured in the repository .env file.");
string embeddingModel = Environment.GetEnvironmentVariable("OPENAI_EMBEDDING_MODEL") ?? "text-embedding-3-small";
string chatModel = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL_NAME") ?? "gpt-4o-mini";

// The reusable utilities own document ingestion, chunking, lexical retrieval, and context rendering.
using var sourceClient = new HttpClient();
var source = new GitHubZipDocumentSource(sourceClient, "evidentlyai", "docs", "main");
RagCorpus corpus = await BuildCorpusAsync(source);
using var lexicalIndex = corpus.LexicalIndex;
OpenAIClient client = new(apiKey);
EmbeddingClient embeddings = client.GetEmbeddingClient(embeddingModel);

const string question = "How can I use an LLM as a judge?";
IReadOnlyList<RagCandidate> candidates = await GetCandidatesAsync(corpus, question, embeddings);
RagFormattedContext context = BuildContext(question, candidates);

Console.WriteLine($"Embedding model: {embeddingModel}");
Console.WriteLine($"Hybrid retrieval selected {candidates.Count} unique chunks.");
AIAgent agent = client.GetResponsesClient().AsAIAgent(
    model: chatModel,
    name: "HybridDocumentationRag",
    instructions: "Answer only from the supplied documentation context. If the context is insufficient, say so.");
AgentResponse<RagAnswer> response = await agent.RunAsync<RagAnswer>(context.RenderedContext);
Console.WriteLine($"Found answer: {response.Result.FoundAnswer}");
Console.WriteLine($"Answer type: {response.Result.AnswerType}");
Console.WriteLine(response.Result.Answer);

#pragma warning restore OPENAI001

static async Task<RagCorpus> BuildCorpusAsync(IDocumentSource source)
{
    IReadOnlyList<IngestedDocument> documents = (await source.ReadAsync()).Select(DocumentMapper.Map).ToList();
    RagCandidate[] candidates = documents.SelectMany(document =>
        CharacterWindowChunker.Chunk(document.Filename, document.Content, new CharacterWindowChunkingOptions(3000, 1500), document.Title)
            .Select(chunk => new RagCandidate(chunk.ChunkId, document.Filename, document.Title, chunk.Text, 0, 0)))
        .ToArray();
    var index = new InMemoryMinSearchIndex(new MinSearchIndexOptions(["title", "description", "content"], ["filename"]));
    await index.FitAsync(candidates.Select(candidate => new MinSearchDocument(candidate.ChunkId, new Dictionary<string, string>
    {
        ["title"] = candidate.Title, ["description"] = "", ["content"] = candidate.Text, ["filename"] = candidate.Filename
    })));
    return new RagCorpus(index, candidates);
}

static async Task<IReadOnlyList<RagCandidate>> GetCandidatesAsync(RagCorpus corpus, string question, EmbeddingClient embeddings)
{
    // Lexical and vector retrieval are independent: each starts from the complete indexed corpus.
    MinSearchResponse lexical = await corpus.LexicalIndex.SearchAsync(new MinSearchQuery(question, resultLimit: 5));
    IReadOnlyList<RagCandidate> lexicalRanked = lexical.Hits.Select(ToCandidate).ToList();

    // This lesson keeps vector storage and ranking example-local. It does not introduce a shared vector utility.
    ReadOnlyMemory<float> queryVector = (await embeddings.GenerateEmbeddingAsync(question)).Value.ToFloats();
    var vectorRanked = new List<RagCandidate>(corpus.Candidates.Count);
    foreach (RagCandidate document in corpus.Candidates)
    {
        ReadOnlyMemory<float> documentVector = (await embeddings.GenerateEmbeddingAsync($"{document.Title} {document.Text}")).Value.ToFloats();
        vectorRanked.Add(document with { VectorScore = CosineSimilarity(queryVector.Span, documentVector.Span) });
    }

    return MergeHybridResults(lexicalRanked, vectorRanked.OrderByDescending(candidate => candidate.VectorScore).Take(5), resultLimit: 5);
}

static IReadOnlyList<RagCandidate> MergeHybridResults(
    IEnumerable<RagCandidate> lexicalRanked,
    IEnumerable<RagCandidate> vectorRanked,
    int resultLimit) =>
    vectorRanked.Concat(lexicalRanked)
        .GroupBy(candidate => candidate.ChunkId, StringComparer.Ordinal)
        .Select(group => group.First())
        .Take(resultLimit)
        .ToList();

static RagFormattedContext BuildContext(string question, IEnumerable<RagCandidate> candidates) =>
    RagContextFormatter.Format(question, candidates.Select(candidate => new RagRetrievalRecord(
        candidate.ChunkId, candidate.Filename, 0, candidate.Text,
        new ChunkProvenance(candidate.Filename, 0, candidate.Text.Length, candidate.Title))),
        "<QUESTION>\n{question}\n</QUESTION>\n\n<CONTEXT>\n{context}\n</CONTEXT>");

static RagCandidate ToCandidate(MinSearchHit hit)
{
    var fields = hit.Document.Fields;
    return new RagCandidate(hit.Document.Id, fields["filename"], fields["title"], fields["content"], hit.Score, 0);
}

static double CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
{
    if (left.Length != right.Length || left.Length == 0) throw new ArgumentException("Embedding vectors must have the same non-zero length.");
    double dot = 0, leftLength = 0, rightLength = 0;
    for (int index = 0; index < left.Length; index++) { dot += left[index] * right[index]; leftLength += left[index] * left[index]; rightLength += right[index] * right[index]; }
    return dot / (Math.Sqrt(leftLength) * Math.Sqrt(rightLength));
}

static async Task RunOfflineFixtureAsync()
{
    using var index = new InMemoryMinSearchIndex(new MinSearchIndexOptions(["title", "description", "content"], ["filename"]));
    await index.FitAsync([
        new MinSearchDocument("judge", new Dictionary<string, string> { ["title"] = "LLM judge", ["description"] = "", ["content"] = "Evaluate an answer with an LLM as a judge.", ["filename"] = "judge.md" }),
        new MinSearchDocument("bread", new Dictionary<string, string> { ["title"] = "Bread", ["description"] = "", ["content"] = "Bake pretzels.", ["filename"] = "bread.md" })]);
    MinSearchResponse lexical = await index.SearchAsync(new MinSearchQuery("LLM judge", resultLimit: 1));
    var lexicalCandidates = lexical.Hits.Select(ToCandidate).ToList();
    RagCandidate semanticOnly = new("bread", "bread.md", "Bread", "Bake pretzels.", 0, CosineSimilarity([1f, 0f], [1f, 0f]));
    var merged = MergeHybridResults(lexicalCandidates, [semanticOnly], resultLimit: 5);
    RagFormattedContext context = BuildContext("LLM judge", merged);
    if (lexicalCandidates.Single().ChunkId != "judge" ||
        !merged.Any(candidate => candidate.ChunkId == "bread") ||
        !context.RenderedContext.Contains("bread.md", StringComparison.Ordinal))
        throw new InvalidOperationException("Offline independent ranking, merge, or context fixture failed.");
    Console.WriteLine("SUCCESS: offline lexical and semantic-only vector candidates survived hybrid merging and context rendering.");
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

// The corpus keeps the fitted lexical index paired with every candidate so vector ranking can start from the same complete set.
sealed record RagCorpus(InMemoryMinSearchIndex LexicalIndex, IReadOnlyList<RagCandidate> Candidates);
// Each candidate retains its chunk identity and source text alongside the lexical and vector scores used by hybrid merging.
sealed record RagCandidate(string ChunkId, string Filename, string Title, string Text, double LexicalScore, double VectorScore);
// These properties are the JSON fields the typed Responses result must populate for the lesson's grounded-answer contract.
public sealed class RagAnswer
{
    // The answer text to display when FoundAnswer is true; it should be grounded in the retrieved context.
    [JsonPropertyName("answer")] public required string Answer { get; init; }
    // True means the context supports an answer; false selects the not-found response path.
    [JsonPropertyName("foundAnswer")] public required bool FoundAnswer { get; init; }
    // A numeric confidence estimate that lets callers show how strongly the model supports the answer.
    [JsonPropertyName("confidence")] public required double Confidence { get; init; }
    // A label for the form of answer produced, such as an explanation or a refusal due to missing evidence.
    [JsonPropertyName("answerType")] public required string AnswerType { get; init; }
    // Follow-up questions that identify missing context or clarify an ambiguous request; an answer may return none.
    [JsonPropertyName("followupQuestions")] public required IReadOnlyList<string> FollowupQuestions { get; init; }
}
