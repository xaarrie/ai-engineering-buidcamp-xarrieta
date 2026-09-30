#:sdk Microsoft.NET.Sdk
#:project ../../utilities/DocumentIngestion/DocumentIngestion.csproj

// Lesson guide: this runnable script keeps the package directives, environment configuration, and framework agent boundary visible. The local records/classes describe input and output contracts; AsAIAgent constructs the agent and RunAsync or RunStreamingAsync is the deliberate model boundary. Offline fixtures, where present, exercise deterministic local behavior without live model calls. The lesson is intentionally scoped to its named concept rather than production retries, semantic embeddings, or transport policy.

using DocumentIngestion;

const string repositoryOwner = "evidentlyai";
const string repositoryName = "docs";
const string revision = "main";

// RAG v4 begins with a repository reader. The Python notebook used GitSource;
// this standalone .NET lesson deliberately uses the native GitHubZipDocumentSource
// rather than inventing an external GitSource equivalent.
Console.WriteLine($"Source configuration: GitHub {repositoryOwner}/{repositoryName} at {revision}.");

using var httpClient = new HttpClient();
IDocumentSource source = new GitHubZipDocumentSource(httpClient, repositoryOwner, repositoryName, revision);
IReadOnlyList<SourceDocument> sourceDocuments = await source.ReadAsync();

Console.WriteLine($"Repository reader returned {sourceDocuments.Count} Markdown/MDX source documents.");
foreach (SourceDocument sourceDocument in sourceDocuments.Take(3))
{
    Console.WriteLine($"Source record: {sourceDocument.Path}");
    Console.WriteLine($"  Raw content preview: {Preview(sourceDocument.Content)}");
}

// Preparing every returned record separates its frontmatter metadata from its content.
IReadOnlyList<IngestedDocument> documents = sourceDocuments.Select(DocumentMapper.Map).ToList();
Console.WriteLine($"Prepared {documents.Count} metadata-parsed documents.");
foreach (IngestedDocument document in documents.Take(3))
{
    Console.WriteLine($"Parsed record: {document.Filename}");
    Console.WriteLine($"  Title: {document.Title}");
    Console.WriteLine($"  Description: {document.Description}");
    Console.WriteLine($"  Content preview: {Preview(document.Content)}");
}

Console.WriteLine("This lesson covers ingestion and preparation only: no retrieval, embeddings, or generation.");

static string Preview(string value) => value.ReplaceLineEndings(" ").Trim() is { Length: > 120 } text
    ? $"{text[..120]}..."
    : value.ReplaceLineEndings(" ").Trim();
