#:sdk Microsoft.NET.Sdk
#:project ../../utilities/DocumentIngestion/DocumentIngestion.csproj

// Lesson guide: this runnable script keeps the package directives, environment configuration, and framework agent boundary visible. The local records/classes describe input and output contracts; AsAIAgent constructs the agent and RunAsync or RunStreamingAsync is the deliberate model boundary. Offline fixtures, where present, exercise deterministic local behavior without live model calls. The lesson is intentionally scoped to its named concept rather than production retries, semantic embeddings, or transport policy.

using DocumentIngestion;

const string repositoryOwner = "evidentlyai";
const string repositoryName = "docs";
const string branch = "main";

// This lesson only ingests and prepares source documents for later indexing.
// Chunking, embeddings, storage, retrieval, and answer generation are out of scope.
Console.WriteLine($"Source configuration: GitHub {repositoryOwner}/{repositoryName} at {branch}.");

using var httpClient = new HttpClient();
IDocumentSource source = new GitHubZipDocumentSource(httpClient, repositoryOwner, repositoryName, branch);
var sourceDocuments = await source.ReadAsync();

Console.WriteLine("Document entries 20 through 29 after archive-path normalization:");
foreach (SourceDocument document in sourceDocuments.Skip(20).Take(10))
{
    Console.WriteLine(document.Path);
}

var documents = sourceDocuments.Select(DocumentMapper.Map).ToList();
Console.WriteLine($"Downloaded {documents.Count} Markdown documents.");
Console.WriteLine("Chunking, embeddings, storage, retrieval, and answer generation are out of scope for this lesson.");

// Every document has already been parsed and is ready for a later indexing lesson.
IReadOnlyList<IngestedDocument> documentsForLaterIndexing = documents;
Console.WriteLine($"Parsed {documentsForLaterIndexing.Count} documents for later indexing.");
