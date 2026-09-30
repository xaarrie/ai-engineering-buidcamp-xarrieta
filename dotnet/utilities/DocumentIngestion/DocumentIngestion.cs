using System.Collections.ObjectModel;
using System.IO.Compression;
using System.Text;

namespace DocumentIngestion;

// Module lesson: ingestion is a trust boundary. Sources return normalized relative Markdown/MDX paths; recursive local files and ZIP entries are filtered and deterministically ordered before mapping. ArchivePaths strips only a proven common root, while path validation rejects rooted, traversal, and drive-like segments. The frontmatter parser is intentionally a limited key/value reader, not YAML, and DocumentMapper is the explicit metadata-to-index boundary.

/// <summary>Represents one Markdown or MDX source after its path has been normalized to a safe relative form.</summary>
public sealed record SourceDocument
{
/// <summary>Creates a document, rejecting unsafe rooted or traversal paths and null content.</summary>
    /// <param name="path">A relative path normalized to forward slashes.</param>
    /// <param name="content">Complete source text; empty content is allowed.</param>
    public SourceDocument(string path, string content)
    {
        Path = MarkdownPaths.NormalizeRelativePath(path);
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>Gets the normalized slash-separated relative path.</summary>
    public string Path { get; }

    /// <summary>Gets the original source text.</summary>
    public string Content { get; }
}

/// <summary>Contains metadata and body text produced by the intentionally limited frontmatter grammar.</summary>
public sealed record ParsedFrontmatter
{
/// <summary>Copies metadata into a detached case-insensitive read-only map and stores the parsed body.</summary>
    public ParsedFrontmatter(IReadOnlyDictionary<string, string> metadata, string content)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Metadata = new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase));
        Content = content ?? throw new ArgumentNullException(nameof(content));
    }

    /// <summary>Gets a detached, read-only, case-insensitive metadata map.</summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>Gets the parsed body, which may be empty.</summary>
    public string Content { get; }
}

/// <summary>Represents the mapper's stable indexing shape, with empty defaults for missing title and description.</summary>
/// <param name="Title">Frontmatter title or an empty string.</param>
/// <param name="Description">Frontmatter description or an empty string.</param>
/// <param name="Content">Body after recognized frontmatter.</param>
/// <param name="Filename">Normalized source-relative path.</param>
public sealed record IngestedDocument(string Title, string Description, string Content, string Filename);

/// <summary>Defines an asynchronous source that returns supported documents with normalized paths and honors cancellation during I/O and enumeration.</summary>
public interface IDocumentSource
{
    /// <summary>Reads documents, checking cancellation during enumeration and I/O.</summary>
    /// <returns>A read-only document list; ordering follows the source implementation (local directories use ordinal path order, while ZIP sources preserve archive-entry order).</returns>
    Task<IReadOnlyList<SourceDocument>> ReadAsync(CancellationToken cancellationToken = default);
}

/// <summary>Recursively reads local Markdown and MDX files beneath one directory.</summary>
public sealed class LocalDirectoryDocumentSource : IDocumentSource
{
    private readonly string _directoryPath;

/// <summary>Creates a source rooted at the full path represented by <paramref name="directoryPath"/>.</summary>
    /// <param name="directoryPath">A nonblank path; relative paths are resolved against the current directory.</param>
    public LocalDirectoryDocumentSource(string directoryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryPath);
        _directoryPath = Path.GetFullPath(directoryPath);
    }

/// <summary>Recursively enumerates files, keeps only Markdown/MDX extensions, and orders paths ordinally before reading.</summary>
    /// <returns>A read-only list, or a <see cref="DirectoryNotFoundException"/> when the root is absent.</returns>
    public async Task<IReadOnlyList<SourceDocument>> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!Directory.Exists(_directoryPath))
        {
            throw new DirectoryNotFoundException($"The document directory does not exist: {_directoryPath}");
        }

        var documents = new List<SourceDocument>();
        IEnumerable<string> filePaths = Directory.EnumerateFiles(_directoryPath, "*", SearchOption.AllDirectories)
            .Where(MarkdownPaths.IsSupported)
            .OrderBy(path => path, StringComparer.Ordinal);

        foreach (string filePath in filePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string content = await File.ReadAllTextAsync(filePath, cancellationToken);
            string relativePath = Path.GetRelativePath(_directoryPath, filePath);
            documents.Add(new SourceDocument(relativePath, content));
        }

        return documents.AsReadOnly();
    }
}

/// <summary>Downloads a GitHub branch ZIP and exposes its supported entries as safe relative documents.</summary>
public sealed class GitHubZipDocumentSource : IDocumentSource
{
    private readonly HttpClient _httpClient;
    private readonly string _owner;
    private readonly string _repository;
    private readonly string _revision;

/// <summary>Creates a source while constraining owner/repository to one URL path segment and revision to non-traversing segments.</summary>
    /// <param name="httpClient">HTTP client used for download; this source does not dispose it.</param>
    public GitHubZipDocumentSource(HttpClient httpClient, string owner, string repository, string revision)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _owner = ValidateSegment(owner, nameof(owner));
        _repository = ValidateSegment(repository, nameof(repository));
        _revision = ValidateRevision(revision);
    }

/// <summary>Downloads, filters, and reads non-directory Markdown/MDX entries; normalized paths remain paired with archive entries.</summary>
    /// <returns>A read-only list in archive entry order.</returns>
    public async Task<IReadOnlyList<SourceDocument>> ReadAsync(CancellationToken cancellationToken = default)
    {
        byte[] archiveBytes = await _httpClient.GetByteArrayAsync(BuildArchiveUri(), cancellationToken);
        using var archiveStream = new MemoryStream(archiveBytes);
        using var archive = new ZipArchive(archiveStream, ZipArchiveMode.Read);

        List<ZipArchiveEntry> entries = archive.Entries
            .Where(entry => !string.IsNullOrEmpty(entry.Name) && MarkdownPaths.IsSupported(entry.FullName))
            .ToList();
        IReadOnlyList<string> paths = ArchivePaths.Normalize(entries.Select(entry => entry.FullName));

        var documents = new List<SourceDocument>(entries.Count);
        for (int index = 0; index < entries.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using Stream entryStream = entries[index].Open();
            using var reader = new StreamReader(entryStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            documents.Add(new SourceDocument(paths[index], await reader.ReadToEndAsync(cancellationToken)));
        }

        return documents.AsReadOnly();
    }

/// <summary>Builds the escaped HTTPS GitHub branch-archive URI.</summary>
    public Uri BuildArchiveUri() => new(
        $"https://github.com/{Uri.EscapeDataString(_owner)}/{Uri.EscapeDataString(_repository)}/archive/refs/heads/{Uri.EscapeDataString(_revision)}.zip");

    private static string ValidateSegment(string value, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);
        if (value.Contains('/') || value.Contains('\\') || value is "." or "..")
        {
            throw new ArgumentException("The value must be a single path segment.", parameterName);
        }

        return value;
    }

    private static string ValidateRevision(string revision)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revision);
        string normalized = revision.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.Split('/').Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("The revision must not contain traversal segments.", nameof(revision));
        }

        return normalized;
    }
}

/// <summary>Filters supported Markdown extensions and enforces safe relative path identities.</summary>
public static class MarkdownPaths
{
/// <summary>Returns <see langword="true"/> only for paths ending in <c>.md</c> or <c>.mdx</c>, ignoring case.</summary>
    public static bool IsSupported(string path) =>
        path.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ||
        path.EndsWith(".mdx", StringComparison.OrdinalIgnoreCase);

/// <summary>Converts backslashes to slashes and removes empty segments, rejecting rooted, drive-like, <c>.</c>, and <c>..</c> segments.</summary>
    /// <returns>A safe normalized relative path.</returns>
    public static string NormalizeRelativePath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        string normalized = path.Replace('\\', '/');
        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            throw new InvalidDataException("Document paths must be relative.");
        }

        string[] segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".." || segment.Contains(':')))
        {
            throw new InvalidDataException("Document paths must not contain unsafe traversal or rooted segments.");
        }

        return string.Join('/', segments);
    }
}

/// <summary>Normalizes archive names and strips one common extraction root only when every entry proves it.</summary>
public static class ArchivePaths
{
/// <summary>Returns normalized paths in input order; a root is removed only when all entries share it and retain a child segment.</summary>
    public static IReadOnlyList<string> Normalize(IEnumerable<string> archivePaths)
    {
        ArgumentNullException.ThrowIfNull(archivePaths);
        List<string> normalized = archivePaths.Select(MarkdownPaths.NormalizeRelativePath).ToList();
        if (normalized.Count < 2)
        {
            return normalized.AsReadOnly();
        }

        string[][] segments = normalized.Select(path => path.Split('/')).ToArray();
        string root = segments[0][0];
        bool commonRoot = segments.All(path => path.Length > 1 && string.Equals(path[0], root, StringComparison.Ordinal));
        if (!commonRoot)
        {
            return normalized.AsReadOnly();
        }

        return segments.Select(path => string.Join('/', path.Skip(1))).ToList().AsReadOnly();
    }
}

/// <summary>Parses a deliberately small delimiter-wrapped <c>key: value</c> grammar, not general YAML.</summary>
public static class BasicYamlFrontmatterParser
{
/// <summary>Accepts a leading and closing line exactly equal to <c>---</c>; malformed lines are ignored and matching quote edges are trimmed.</summary>
    /// <remarks>An unterminated block returns empty metadata and the original source unchanged.</remarks>
    public static ParsedFrontmatter Parse(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var reader = new StringReader(source);
        if (!string.Equals(reader.ReadLine(), "---", StringComparison.Ordinal))
        {
            return new ParsedFrontmatter(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), source);
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (string.Equals(line, "---", StringComparison.Ordinal))
            {
                return new ParsedFrontmatter(metadata, reader.ReadToEnd());
            }

            int separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            string key = line[..separatorIndex].Trim();
            string value = line[(separatorIndex + 1)..].Trim().Trim('"', '\'');
            metadata[key] = value;
        }

        return new ParsedFrontmatter(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), source);
    }
}

/// <summary>Maps normalized source metadata and body text into the lexical indexing shape.</summary>
public static class DocumentMapper
{
/// <summary>Uses empty strings for missing <c>title</c> and <c>description</c>, while retaining the source path as <c>Filename</c>.</summary>
    public static IngestedDocument Map(SourceDocument source)
    {
        ArgumentNullException.ThrowIfNull(source);
        ParsedFrontmatter parsed = BasicYamlFrontmatterParser.Parse(source.Content);
        return new IngestedDocument(
            parsed.Metadata.GetValueOrDefault("title", string.Empty),
            parsed.Metadata.GetValueOrDefault("description", string.Empty),
            parsed.Content,
            source.Path);
    }
}
