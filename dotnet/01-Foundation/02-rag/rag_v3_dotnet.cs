#:sdk Microsoft.NET.Sdk
#:project ../../utilities/DocumentIngestion/DocumentIngestion.csproj

// Lesson guide: this runnable script keeps the package directives, environment configuration, and framework agent boundary visible. The local records/classes describe input and output contracts; AsAIAgent constructs the agent and RunAsync or RunStreamingAsync is the deliberate model boundary. Offline fixtures, where present, exercise deterministic local behavior without live model calls. The lesson is intentionally scoped to its named concept rather than production retries, semantic embeddings, or transport policy.

using DocumentIngestion;

const string repositoryOwner = "evidentlyai";
const string repositoryName = "docs";
const string revision = "main";

// RAG v3 starts with the raw GitHub archive documents. The reusable source owns
// the download, archive filtering, and archive-path normalization.
Console.WriteLine($"Source configuration: GitHub {repositoryOwner}/{repositoryName} at {revision}.");

using var httpClient = new HttpClient();
IDocumentSource source = new GitHubZipDocumentSource(httpClient, repositoryOwner, repositoryName, revision);
IReadOnlyList<SourceDocument> sourceDocuments = await source.ReadAsync();

Console.WriteLine($"Loaded {sourceDocuments.Count} Markdown/MDX source documents.");
foreach (SourceDocument sourceDocument in sourceDocuments.Take(3))
{
    ParsedFrontmatter frontmatter = BasicYamlFrontmatterParser.Parse(sourceDocument.Content);
    Console.WriteLine($"Raw source: {sourceDocument.Path}");
    Console.WriteLine($"  Frontmatter title: {frontmatter.Metadata.GetValueOrDefault("title", "(missing)")}");
    Console.WriteLine($"  Frontmatter description: {frontmatter.Metadata.GetValueOrDefault("description", "(missing)")}");
}

// Map the source records into the stable preparation contract used by later lessons.
IReadOnlyList<IngestedDocument> documents = sourceDocuments.Select(DocumentMapper.Map).ToList();
Console.WriteLine($"Prepared {documents.Count} metadata-parsed documents.");
foreach (IngestedDocument document in documents.Take(3))
{
    Console.WriteLine($"Prepared record: {document.Filename}");
    Console.WriteLine($"  Title: {document.Title}");
    Console.WriteLine($"  Description: {document.Description}");
    Console.WriteLine($"  Content preview: {Preview(document.Content)}");
}

// The notebook later compares this workflow with Python GitSource. This lesson does
// not invent an external .NET GitSource equivalent: GitHubZipDocumentSource is the
// explicit native source used for the same ingestion/preparation boundary.
Console.WriteLine("This lesson covers ingestion and preparation only: no retrieval, embeddings, or generation.");

static string Preview(string value) => value.ReplaceLineEndings(" ").Trim() is { Length: > 120 } text
    ? $"{text[..120]}..."
    : value.ReplaceLineEndings(" ").Trim();
