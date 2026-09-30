// Fixture lesson: tests pin character-window overlap/final partial windows, stable provenance IDs, deterministic ordering, JSON escaping, template boundaries, and the deliberate platform-only dependency scope.

using System.Reflection;
using System.Text.Json;
using RagSupport;

var assertions = new (string Name, Action Test)[]
{
    ("creates deterministic character windows", CharacterWindows),
    ("handles empty and short inputs", EmptyAndShortInputs),
    ("preserves stable IDs and provenance", StableIdsAndProvenance),
    ("rejects invalid chunking configuration", InvalidConfiguration),
    ("serializes retrieval records deterministically", DeterministicJsonAndOrdering),
    ("escapes JSON and renders caller template", EscapingAndRendering),
    ("retains no external dependencies", ExternalDependencyNeutrality)
};

try
{
    foreach (var assertion in assertions)
    {
        assertion.Test();
    }

    Console.WriteLine($"SUCCESS: {assertions.Length} RagSupport assertions passed.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL: {exception.Message}");
    return 1;
}

static void CharacterWindows()
{
    var chunks = CharacterWindowChunker.Chunk("source", "abcdef", new CharacterWindowChunkingOptions(3, 2));
    Require(chunks.Select(chunk => chunk.Text).SequenceEqual(["abc", "cde", "ef"]), "Window boundaries were incorrect.");
    Require(chunks.Select(chunk => chunk.Provenance.StartIndex).SequenceEqual([0, 2, 4]), "Window starts were incorrect.");
    Require(chunks.Select(chunk => chunk.Provenance.EndIndex).SequenceEqual([3, 5, 6]), "Window ends were incorrect.");
}

static void EmptyAndShortInputs()
{
    Require(CharacterWindowChunker.Chunk("source", string.Empty, new CharacterWindowChunkingOptions(3, 1)).Count == 0, "Empty input produced chunks.");
    var chunks = CharacterWindowChunker.Chunk("source", "ab", new CharacterWindowChunkingOptions(3, 1));
    Require(chunks.Count == 2 && chunks[0].Text == "ab" && chunks[1].Text == "b", "Short input windows were incorrect.");
}

static void StableIdsAndProvenance()
{
    var options = new CharacterWindowChunkingOptions(3, 2);
    var first = CharacterWindowChunker.Chunk("source", "abcdef", options, "origin");
    var second = CharacterWindowChunker.Chunk("source", "abcdef", options, "origin");
    Require(first.Select(chunk => chunk.ChunkId).SequenceEqual(second.Select(chunk => chunk.ChunkId)), "Chunk IDs were not stable.");
    Require(first.All(chunk => chunk.SourceIdentifier == "source" && chunk.Provenance.SourceIdentifier == "source" && chunk.Provenance.Label == "origin"), "Source provenance was not preserved.");
}

static void InvalidConfiguration()
{
    RequireThrows<ArgumentOutOfRangeException>(() => CharacterWindowChunker.Chunk("source", "text", new CharacterWindowChunkingOptions(0, 1)));
    RequireThrows<ArgumentOutOfRangeException>(() => CharacterWindowChunker.Chunk("source", "text", new CharacterWindowChunkingOptions(1, 0)));
    RequireThrows<ArgumentException>(() => CharacterWindowChunker.Chunk(" ", "text", new CharacterWindowChunkingOptions(1, 1)));
}

static void DeterministicJsonAndOrdering()
{
    var later = new RagRetrievalRecord("b", "source-b", 1, "later");
    var first = new RagRetrievalRecord("a", "source-a", 0, "first", new ChunkProvenance("source-a", 0, 5, null));
    var forward = RagContextFormatter.Serialize([later, first]);
    var reverse = RagContextFormatter.Serialize([first, later]);
    Require(forward == reverse, "JSON record ordering was not deterministic.");
    Require(forward.StartsWith("[{\"chunkId\":\"a\"", StringComparison.Ordinal), "JSON ordering did not use stable record keys.");
}

static void EscapingAndRendering()
{
    var record = new RagRetrievalRecord("id", "source", 0, "quote \" slash \\ line\n");
    var formatted = RagContextFormatter.Format("what \"now\"?", [record], "Question={question}; Context={context}");
    using var document = JsonDocument.Parse(formatted.ContextJson);
    Require(document.RootElement[0].GetProperty("text").GetString() == record.Text, "JSON escaping was incorrect.");
    Require(formatted.RenderedContext.StartsWith("Question=what \"now\"?; Context=", StringComparison.Ordinal), "Caller template question replacement was incorrect.");
    RequireThrows<ArgumentException>(() => RagContextFormatter.Format("question", [record], "{question}"));
}

static void ExternalDependencyNeutrality()
{
    var assemblyReferences = typeof(RagChunk).Assembly.GetReferencedAssemblies().Select(reference => reference.Name).ToArray();
    Require(assemblyReferences.All(name => name is not null && (name.StartsWith("System", StringComparison.Ordinal) || name == "netstandard")), "The library retained a non-platform assembly dependency.");

    var projectPath = FindProjectFile();
    var project = File.ReadAllText(projectPath);
    Require(!project.Contains("PackageReference", StringComparison.Ordinal) && !project.Contains("ProjectReference", StringComparison.Ordinal), "The library project retained an external dependency reference.");
}

static string FindProjectFile()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        var candidate = Path.Combine(directory.FullName, "dotnet", "utilities", "RagSupport", "RagSupport.csproj");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not locate RagSupport.csproj.");
}

static void RequireThrows<TException>(Action action)
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

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}

static void Require(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
