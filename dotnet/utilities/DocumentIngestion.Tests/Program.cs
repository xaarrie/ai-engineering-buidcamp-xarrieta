// Fixture lesson: ZIP tests prove common-root stripping only when every entry shares the root; traversal tests protect the source trust boundary; local fixtures prove recursive filtering and slash-normalized relative names.

using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text;
using DocumentIngestion;

var tests = new (string Name, Func<Task> Execute)[]
{
    ("Markdown and MDX paths are supported", SupportedPathsAsync),
    ("basic frontmatter cases are preserved", FrontmatterCasesAsync),
    ("mapping applies empty metadata defaults", MappingDefaultsAsync),
    ("ZIP paths strip a proven common root", ZipCommonRootAsync),
    ("ZIP paths retain paths without a common root", ZipNoCommonRootAsync),
    ("unsafe archive traversal paths are rejected", TraversalRejectionAsync),
    ("local source returns normalized relative paths", LocalRelativePathsAsync)
};

try
{
    foreach (var test in tests)
    {
        await test.Execute();
        Console.WriteLine($"PASS: {test.Name}");
    }

    Console.WriteLine($"SUCCESS: {tests.Length} DocumentIngestion assertions passed.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL: {exception.Message}");
    return 1;
}

static Task SupportedPathsAsync()
{
    True(MarkdownPaths.IsSupported("guide.md"), "Markdown files must be supported.");
    True(MarkdownPaths.IsSupported("guide.MDX"), "MDX files must be supported case-insensitively.");
    True(!MarkdownPaths.IsSupported("guide.txt"), "Non-Markdown files must be excluded.");
    Equal("guides/intro.mdx", MarkdownPaths.NormalizeRelativePath("guides\\intro.mdx"), "Separators must normalize.");
    return Task.CompletedTask;
}

static Task FrontmatterCasesAsync()
{
    ParsedFrontmatter parsed = BasicYamlFrontmatterParser.Parse("---\ntitle: 'Example'\ndescription: \"Short\"\n---\nBody");
    Equal("Example", parsed.Metadata["title"], "Quoted title values must be parsed.");
    Equal("Short", parsed.Metadata["description"], "Quoted description values must be parsed.");
    Equal("Body", parsed.Content, "Content after a closing marker must be retained.");

    ParsedFrontmatter unclosed = BasicYamlFrontmatterParser.Parse("---\ntitle: Example\nBody");
    Equal(0, unclosed.Metadata.Count, "Unclosed frontmatter must remain ordinary content.");
    Equal("---\ntitle: Example\nBody", unclosed.Content, "Unclosed frontmatter content must be unchanged.");
    return Task.CompletedTask;
}

static Task MappingDefaultsAsync()
{
    IngestedDocument document = DocumentMapper.Map(new SourceDocument("guides/example.md", "Body"));
    Equal(string.Empty, document.Title, "Missing titles must map to an empty value.");
    Equal(string.Empty, document.Description, "Missing descriptions must map to an empty value.");
    Equal("Body", document.Content, "Document content must be retained.");
    Equal("guides/example.md", document.Filename, "The normalized source path must become the filename.");
    return Task.CompletedTask;
}

static async Task ZipCommonRootAsync()
{
    byte[] fixture = CreateZip(("archive-root/guides/first.md", "First"), ("archive-root/second.mdx", "Second"));
    using var client = CreateFixtureClient(fixture);
    var source = new GitHubZipDocumentSource(client, "sample-owner", "sample-repository", "sample-revision");

    IReadOnlyList<SourceDocument> documents = await source.ReadAsync();
    Equal("guides/first.md", documents[0].Path, "A proven archive root must be stripped.");
    Equal("second.mdx", documents[1].Path, "A proven archive root must be stripped from every entry.");
}

static async Task ZipNoCommonRootAsync()
{
    byte[] fixture = CreateZip(("guides/first.md", "First"), ("reference/second.mdx", "Second"));
    using var client = CreateFixtureClient(fixture);
    var source = new GitHubZipDocumentSource(client, "sample-owner", "sample-repository", "sample-revision");

    IReadOnlyList<SourceDocument> documents = await source.ReadAsync();
    Equal("guides/first.md", documents[0].Path, "Paths without a proven common root must remain unchanged.");
    Equal("reference/second.mdx", documents[1].Path, "Paths without a proven common root must remain unchanged.");
}

static Task TraversalRejectionAsync()
{
    Throws<InvalidDataException>(() => ArchivePaths.Normalize(["archive-root/../outside.md", "archive-root/safe.md"]));
    Throws<InvalidDataException>(() => MarkdownPaths.NormalizeRelativePath("../outside.md"));
    return Task.CompletedTask;
}

static async Task LocalRelativePathsAsync()
{
    string directory = Path.Combine(Path.GetTempPath(), $"document-ingestion-{Guid.NewGuid():N}");
    Directory.CreateDirectory(Path.Combine(directory, "nested"));
    try
    {
        await File.WriteAllTextAsync(Path.Combine(directory, "nested", "guide.md"), "Guide");
        await File.WriteAllTextAsync(Path.Combine(directory, "ignored.txt"), "Ignored");
        var source = new LocalDirectoryDocumentSource(directory);

        IReadOnlyList<SourceDocument> documents = await source.ReadAsync();
        Equal(1, documents.Count, "Only supported Markdown paths must be loaded from local directories.");
        Equal("nested/guide.md", documents[0].Path, "Local paths must be relative and slash-normalized.");
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static byte[] CreateZip(params (string Path, string Content)[] entries)
{
    using var stream = new MemoryStream();
    using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
    {
        foreach ((string path, string content) in entries)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path);
            using StreamWriter writer = new(entry.Open(), Encoding.UTF8, leaveOpen: false);
            writer.Write(content);
        }
    }

    return stream.ToArray();
}

static HttpClient CreateFixtureClient(byte[] content) => new(new FixtureHandler(content));

static void Equal<T>(T expected, T actual, string message)
    where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
    }
}

static void True(bool value, string message)
{
    if (!value)
    {
        throw new InvalidOperationException(message);
    }
}

static void Throws<TException>(Action action)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name} was not thrown.");
}

sealed class FixtureHandler(byte[] content) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(content) });
}
