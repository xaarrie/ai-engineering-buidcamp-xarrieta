#:sdk Microsoft.NET.Sdk
#:project ../../utilities/DocumentIngestion/DocumentIngestion.csproj
#:project ../../utilities/MinSearch/MinSearch.csproj

// Lesson guide: this runnable script keeps the package directives, environment configuration, and framework agent boundary visible. The local records/classes describe input and output contracts; AsAIAgent constructs the agent and RunAsync or RunStreamingAsync is the deliberate model boundary. Offline fixtures, where present, exercise deterministic local behavior without live model calls. The lesson is intentionally scoped to its named concept rather than production retries, semantic embeddings, or transport policy.

using DocumentIngestion;
using MinSearch;

// Default mode downloads https://github.com/evidentlyai/docs/archive/refs/heads/main.zip.
// GitHub ingestion requires network access. Use --local <directory> for deterministic offline input.
// Usage: dotnet run rag_v2_dotnet.cs -- [--local <directory>] [--owner <owner>] [--repo <repository>] [--branch <branch>] [--query <text>] [--filename <path>]
if (!TryParseArguments(args, out InputOptions input, out string? error))
{
    Console.Error.WriteLine(error);
    WriteUsage();
    Environment.ExitCode = 1;
    return;
}

try
{
    IReadOnlyList<SourceDocument> sourceDocuments;
    string sourceDescription;

    if (input.LocalDirectory is not null)
    {
        IDocumentSource source = new LocalDirectoryDocumentSource(input.LocalDirectory);
        sourceDocuments = await source.ReadAsync();
        sourceDescription = $"local directory '{input.LocalDirectory}' (offline mode; no network access required)";
    }
    else
    {
        using var httpClient = new HttpClient();
        IDocumentSource source = new GitHubZipDocumentSource(httpClient, input.Owner, input.Repository, input.Branch);
        sourceDocuments = await source.ReadAsync();
        sourceDescription = $"GitHub ZIP {input.Owner}/{input.Repository}@{input.Branch} (network access required)";

        Console.WriteLine($"Relevant Markdown/MDX archive entries ({sourceDocuments.Count}); showing up to 10:");
        foreach (SourceDocument document in sourceDocuments.Take(10))
        {
            Console.WriteLine(document.Path);
        }
    }

    if (sourceDocuments.Count == 0)
    {
        Console.Error.WriteLine("No .md or .mdx documents were found in the selected source.");
        Environment.ExitCode = 1;
        return;
    }

    IReadOnlyList<IngestedDocument> lessonDocuments = sourceDocuments.Select(DocumentMapper.Map).ToList();

    var options = new MinSearchIndexOptions(
        textFields: ["title", "description", "content"],
        keywordFields: ["filename"]);

    using var inMemoryIndex = new InMemoryMinSearchIndex(options);
    IMinSearchIndex index = inMemoryIndex;
    await index.FitAsync(lessonDocuments.Select(ToMinSearchDocument));

    var filters = input.ExactFilename is null
        ? new Dictionary<string, string>(StringComparer.Ordinal)
        : new Dictionary<string, string>(StringComparer.Ordinal) { ["filename"] = input.ExactFilename };
    var query = new MinSearchQuery(
        text: input.QueryText,
        exactMatchFilters: filters,
        fieldBoosts: new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["title"] = 3,
            ["description"] = 2,
            ["content"] = 1
        },
        resultLimit: 5);

    MinSearchResponse response = await index.SearchAsync(query);

    Console.WriteLine($"Loaded {lessonDocuments.Count} Markdown/MDX documents from {sourceDescription}.");
    Console.WriteLine($"Lexical query: {query.Text}");
    Console.WriteLine(input.ExactFilename is null
        ? "Exact filename filter: not applied"
        : $"Exact filename filter: {input.ExactFilename}");
    Console.WriteLine($"Provider-neutral ranked results ({response.Hits.Count}):");

    foreach (MinSearchHit hit in response.Hits)
    {
        IReadOnlyDictionary<string, string> fields = hit.Document.Fields;
        Console.WriteLine($"- score={hit.Score:F4} filename={fields["filename"]}");
        Console.WriteLine($"  title={fields["title"]}");
        Console.WriteLine($"  description={fields["description"]}");
    }

    if (response.Hits.Count == 0)
    {
        Console.WriteLine("No lexical matches were found for this query and optional exact filename filter.");
    }

    Console.WriteLine();
    Console.WriteLine("The public MinSearch contracts intentionally avoid Lucene types, so a future Lucene.NET backend can replace the in-memory implementation without changing this lesson's retrieval code.");
    Console.WriteLine("This lesson performs lexical in-memory retrieval only: it does not implement chunking, embeddings, vector search, or answer generation.");
}
catch (HttpRequestException exception)
{
    Console.Error.WriteLine($"Could not download the GitHub ZIP archive: {exception.Message}");
    Environment.ExitCode = 1;
}
catch (InvalidDataException exception)
{
    Console.Error.WriteLine($"Could not read the ZIP archive: {exception.Message}");
    Environment.ExitCode = 1;
}
catch (IOException exception)
{
    Console.Error.WriteLine($"Could not read documents: {exception.Message}");
    Environment.ExitCode = 1;
}
catch (UnauthorizedAccessException exception)
{
    Console.Error.WriteLine($"Could not access local documents: {exception.Message}");
    Environment.ExitCode = 1;
}

static bool TryParseArguments(string[] args, out InputOptions options, out string? error)
{
    const string defaultOwner = "evidentlyai";
    const string defaultRepository = "docs";
    const string defaultBranch = "main";

    string owner = defaultOwner;
    string repository = defaultRepository;
    string branch = defaultBranch;
    string queryText = "retrieval";
    string? localDirectory = null;
    string? exactFilename = null;

    for (int index = 0; index < args.Length; index++)
    {
        string argument = args[index];
        if (argument is not ("--local" or "--owner" or "--repo" or "--branch" or "--query" or "--filename"))
        {
            options = default!;
            error = $"Unknown argument: {argument}";
            return false;
        }

        if (++index >= args.Length || string.IsNullOrWhiteSpace(args[index]))
        {
            options = default!;
            error = $"A value is required for {argument}.";
            return false;
        }

        string value = args[index];
        switch (argument)
        {
            case "--local":
                localDirectory = Path.GetFullPath(value);
                break;
            case "--owner":
                owner = value;
                break;
            case "--repo":
                repository = value;
                break;
            case "--branch":
                branch = value;
                break;
            case "--query":
                queryText = value;
                break;
            case "--filename":
                exactFilename = value.Replace('\\', '/');
                break;
        }
    }

    if (localDirectory is not null && !Directory.Exists(localDirectory))
    {
        options = default!;
        error = $"The local documents directory does not exist: {localDirectory}";
        return false;
    }

    options = new InputOptions(owner, repository, branch, queryText, localDirectory, exactFilename);
    error = null;
    return true;
}

static void WriteUsage()
{
    Console.Error.WriteLine("Usage: dotnet run rag_v2_dotnet.cs -- [--local <directory>] [--owner <owner>] [--repo <repository>] [--branch <branch>] [--query <text>] [--filename <path>]");
    Console.Error.WriteLine("Default source: GitHub ZIP for evidentlyai/docs on main (network access required).");
    Console.Error.WriteLine("Offline example: dotnet run rag_v2_dotnet.cs -- --local ./docs --query retrieval --filename guides/getting-started.md");
    Console.Error.WriteLine("GitHub override example: dotnet run rag_v2_dotnet.cs -- --owner evidentlyai --repo docs --branch main --query retrieval");
}

static MinSearchDocument ToMinSearchDocument(IngestedDocument document) =>
    new(
        id: document.Filename,
        fields: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["title"] = document.Title,
            ["description"] = document.Description,
            ["content"] = document.Content,
            ["filename"] = document.Filename
        });

sealed record InputOptions(
    string Owner,
    string Repository,
    string Branch,
    string QueryText,
    string? LocalDirectory,
    string? ExactFilename);
