using System.Collections.ObjectModel;

namespace MinSearch;

// Module lesson: this educational index separates tokenized text fields from exact keyword fields. Fit replaces an immutable snapshot; Append rebuilds one from the old snapshot plus additions; a reader/writer gate publishes complete snapshots to concurrent readers. TF-IDF cosine scores, positive boosts, and ordinal ID tie-breaking make behavior reproducible.

/// <summary>An immutable document whose configured fields are either tokenized text or exact-match keywords.</summary>
public sealed class MinSearchDocument
{
/// <summary>Creates a document and copies its non-null, nonblank-key fields into a read-only ordinal map.</summary>
    public MinSearchDocument(string id, IReadOnlyDictionary<string, string> fields)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ArgumentNullException.ThrowIfNull(fields);

        Id = id;
        Fields = CopyDictionary(fields, nameof(fields));
    }

    /// <summary>Gets the unique ordinal-comparison identifier; duplicate IDs are rejected during indexing.</summary>
    public string Id { get; }

    /// <summary>Gets a detached read-only field map; missing configured fields behave as empty text during indexing.</summary>
    public IReadOnlyDictionary<string, string> Fields { get; }

    private static IReadOnlyDictionary<string, string> CopyDictionary(
        IReadOnlyDictionary<string, string> source,
        string parameterName)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (key, value) in source)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(key, parameterName);
            ArgumentNullException.ThrowIfNull(value, parameterName);
            copy.Add(key, value);
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }
}

/// <summary>Declares which fields receive TF-IDF text scoring and which remain exact keyword filters.</summary>
public sealed class MinSearchIndexOptions
{
    private readonly HashSet<string> textFieldSet;
    private readonly HashSet<string> keywordFieldSet;

/// <summary>Creates field classifications; duplicates are errors and at least one text field is required.</summary>
    public MinSearchIndexOptions(
        IEnumerable<string> textFields,
        IEnumerable<string>? keywordFields = null)
    {
        ArgumentNullException.ThrowIfNull(textFields);

        textFieldSet = CreateFieldSet(textFields, nameof(textFields));
        keywordFieldSet = CreateFieldSet(keywordFields ?? [], nameof(keywordFields));

        if (textFieldSet.Count == 0)
        {
            throw new ArgumentException("At least one text field is required.", nameof(textFields));
        }

        TextFields = Array.AsReadOnly(textFieldSet.OrderBy(field => field, StringComparer.Ordinal).ToArray());
        KeywordFields = Array.AsReadOnly(keywordFieldSet.OrderBy(field => field, StringComparer.Ordinal).ToArray());
    }

    /// <summary>Gets distinct configured text fields in ordinal order.</summary>
    public IReadOnlyList<string> TextFields { get; }

    /// <summary>Gets distinct configured keyword fields in ordinal order; these fields are never tokenized.</summary>
    public IReadOnlyList<string> KeywordFields { get; }

    internal bool IsTextField(string field) => textFieldSet.Contains(field);

    internal bool IsKeywordField(string field) => keywordFieldSet.Contains(field);

    private static HashSet<string> CreateFieldSet(IEnumerable<string> fields, string parameterName)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach (var field in fields)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field, parameterName);

            if (!result.Add(field))
            {
                throw new ArgumentException($"The field '{field}' is duplicated.", parameterName);
            }
        }

        return result;
    }
}

/// <summary>Describes query text, ordinal exact filters, positive text-field boosts, and the maximum result count.</summary>
public sealed class MinSearchQuery
{
/// <summary>Creates a query and defensively copies filters and boosts; negative limits and nonpositive boosts are invalid.</summary>
    public MinSearchQuery(
        string text,
        IReadOnlyDictionary<string, string>? exactMatchFilters = null,
        IReadOnlyDictionary<string, double>? fieldBoosts = null,
        int resultLimit = 10)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (resultLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(resultLimit), "The result limit cannot be negative.");
        }

        Text = text;
        ExactMatchFilters = CopyFilters(exactMatchFilters ?? new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal)));
        FieldBoosts = CopyBoosts(fieldBoosts ?? new ReadOnlyDictionary<string, double>(new Dictionary<string, double>(StringComparer.Ordinal)));
        ResultLimit = resultLimit;
    }

    /// <summary>Gets query text; letters and digits are lower-cased and split from punctuation.</summary>
    public string Text { get; }

    /// <summary>Gets keyword equality filters; every entry must name a configured keyword field.</summary>
    public IReadOnlyDictionary<string, string> ExactMatchFilters { get; }

    /// <summary>Gets positive finite multipliers for configured text fields; unspecified fields use 1.</summary>
    public IReadOnlyDictionary<string, double> FieldBoosts { get; }

    /// <summary>Gets the maximum hits; zero deliberately returns no hits.</summary>
    public int ResultLimit { get; }

    private static IReadOnlyDictionary<string, string> CopyFilters(IReadOnlyDictionary<string, string> source)
    {
        var copy = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var (field, value) in source)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field, nameof(source));
            ArgumentNullException.ThrowIfNull(value, nameof(source));
            copy.Add(field, value);
        }

        return new ReadOnlyDictionary<string, string>(copy);
    }

    private static IReadOnlyDictionary<string, double> CopyBoosts(IReadOnlyDictionary<string, double> source)
    {
        var copy = new Dictionary<string, double>(StringComparer.Ordinal);

        foreach (var (field, boost) in source)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(field, nameof(source));

            if (double.IsNaN(boost) || double.IsInfinity(boost) || boost <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(source), "Field boosts must be finite and greater than zero.");
            }

            copy.Add(field, boost);
        }

        return new ReadOnlyDictionary<string, double>(copy);
    }
}

/// <summary>Pairs an indexed document with its summed boosted cosine score.</summary>
/// <param name="Document">The original immutable document.</param>
/// <param name="Score">A positive TF-IDF cosine-based score.</param>
public sealed record MinSearchHit(MinSearchDocument Document, double Score);

/// <summary>Contains an ordered, read-only search result snapshot.</summary>
public sealed class MinSearchResponse
{
/// <summary>Copies hits into a read-only response, preserving the caller's enumeration order.</summary>
    public MinSearchResponse(IEnumerable<MinSearchHit> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);
        Hits = Array.AsReadOnly(hits.ToArray());
    }

    /// <summary>Gets hits ordered by descending score, then ordinal document ID.</summary>
    public IReadOnlyList<MinSearchHit> Hits { get; }
}

/// <summary>Defines fit, append, and search operations over immutable published index snapshots.</summary>
public interface IMinSearchIndex
{
    /// <summary>Replaces the complete snapshot; duplicate IDs fail before publication.</summary>
    Task FitAsync(IEnumerable<MinSearchDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>Builds a new snapshot from the current documents plus additions, then publishes it atomically.</summary>
    Task AppendAsync(IEnumerable<MinSearchDocument> documents, CancellationToken cancellationToken = default);

    /// <summary>Reads one immutable snapshot; filters use ordinal equality and ties use ordinal IDs.</summary>
    Task<MinSearchResponse> SearchAsync(MinSearchQuery query, CancellationToken cancellationToken = default);
}

/// <summary>
/// A deterministic in-memory implementation that scores each configured text field independently with TF-IDF cosine similarity,
/// then sums the field scores after applying query boosts. Term frequency is the count divided by the total token count in the
/// field or query; inverse document frequency is <c>log((documentCount + 1) / (documentFrequency + 1)) + 1</c>. Exact keyword
/// filters use ordinal string equality, and equal scores are ordered by document id using ordinal comparison.
/// </summary>
public sealed class InMemoryMinSearchIndex : IMinSearchIndex, IDisposable
{
    private readonly MinSearchIndexOptions options;
    private readonly ReaderWriterLockSlim gate = new();
    private IndexSnapshot snapshot = IndexSnapshot.Empty;
    private bool disposed;

/// <summary>Creates a thread-safe in-memory index; readers see complete immutable snapshots, never partial rebuilds.</summary>
    /// <param name="options">Field classification used for every snapshot.</param>
    public InMemoryMinSearchIndex(MinSearchIndexOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

/// <summary>Builds TF-IDF vectors and atomically replaces the published snapshot.</summary>
    public Task FitAsync(IEnumerable<MinSearchDocument> documents, CancellationToken cancellationToken = default)
    {
        var replacement = BuildSnapshot(documents, cancellationToken);

        gate.EnterWriteLock();
        try
        {
            ThrowIfDisposed();
            snapshot = replacement;
        }
        finally
        {
            gate.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

/// <summary>Reindexes the old snapshot plus additions; duplicate IDs are rejected.</summary>
    public Task AppendAsync(IEnumerable<MinSearchDocument> documents, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var additions = documents.ToArray();

        gate.EnterWriteLock();
        try
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            snapshot = BuildSnapshot(snapshot.Documents.Select(document => document.Document).Concat(additions), cancellationToken);
        }
        finally
        {
            gate.ExitWriteLock();
        }

        return Task.CompletedTask;
    }

/// <summary>Tokenizes query text, computes cosine similarity per text field, applies boosts, and returns stable ties.</summary>
    public Task<MinSearchResponse> SearchAsync(MinSearchQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ValidateQuery(query);
        cancellationToken.ThrowIfCancellationRequested();

        gate.EnterReadLock();
        try
        {
            ThrowIfDisposed();

            if (query.ResultLimit == 0 || snapshot.Documents.Count == 0)
            {
                return Task.FromResult(new MinSearchResponse([]));
            }

            var queryVector = CreateQueryVector(query.Text, snapshot.InverseDocumentFrequency);
            if (queryVector.Norm == 0)
            {
                return Task.FromResult(new MinSearchResponse([]));
            }

            var hits = snapshot.Documents
                .Where(document => MatchesFilters(document.Document, query.ExactMatchFilters))
                .Select(document => new MinSearchHit(document.Document, Score(document, query, queryVector)))
                .Where(hit => hit.Score > 0)
                .OrderByDescending(hit => hit.Score)
                .ThenBy(hit => hit.Document.Id, StringComparer.Ordinal)
                .Take(query.ResultLimit)
                .ToArray();

            return Task.FromResult(new MinSearchResponse(hits));
        }
        finally
        {
            gate.ExitReadLock();
        }
    }

/// <summary>Marks the index disposed, clears its snapshot, and causes later operations to throw.</summary>
    public void Dispose()
    {
        gate.EnterWriteLock();
        try
        {
            if (!disposed)
            {
                disposed = true;
                snapshot = IndexSnapshot.Empty;
            }
        }
        finally
        {
            gate.ExitWriteLock();
        }
    }

    private IndexSnapshot BuildSnapshot(IEnumerable<MinSearchDocument> documents, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var sourceDocuments = documents.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var tokenizedDocuments = new List<TokenizedDocument>(sourceDocuments.Length);
        var documentFrequency = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var document in sourceDocuments)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ArgumentNullException.ThrowIfNull(document);

            if (!ids.Add(document.Id))
            {
                throw new ArgumentException($"The document id '{document.Id}' is duplicated.", nameof(documents));
            }

            var fields = new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal);
            var documentTerms = new HashSet<string>(StringComparer.Ordinal);

            foreach (var field in options.TextFields)
            {
                var terms = Tokenize(document.Fields.GetValueOrDefault(field, string.Empty))
                    .GroupBy(term => term, StringComparer.Ordinal)
                    .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
                fields.Add(field, terms);
                documentTerms.UnionWith(terms.Keys);
            }

            foreach (var term in documentTerms)
            {
                documentFrequency[term] = documentFrequency.GetValueOrDefault(term) + 1;
            }

            tokenizedDocuments.Add(new TokenizedDocument(document, fields));
        }

        var idf = documentFrequency.ToDictionary(
            pair => pair.Key,
            pair => Math.Log((sourceDocuments.Length + 1d) / (pair.Value + 1d)) + 1d,
            StringComparer.Ordinal);
        var indexedDocuments = tokenizedDocuments.Select(document => CreateIndexedDocument(document, idf)).ToArray();
        return new IndexSnapshot(indexedDocuments, new ReadOnlyDictionary<string, double>(idf));
    }

    private static IndexedDocument CreateIndexedDocument(
        TokenizedDocument document,
        IReadOnlyDictionary<string, double> idf)
    {
        var fields = new Dictionary<string, FieldVector>(StringComparer.Ordinal);

        foreach (var (field, terms) in document.Fields)
        {
            var tokenCount = terms.Values.Sum();
            var weights = terms.ToDictionary(
                pair => pair.Key,
                pair => (pair.Value / (double)tokenCount) * idf[pair.Key],
                StringComparer.Ordinal);
            var norm = Math.Sqrt(weights.Values.Sum(weight => weight * weight));
            fields.Add(field, new FieldVector(new ReadOnlyDictionary<string, double>(weights), norm));
        }

        return new IndexedDocument(document.Document, new ReadOnlyDictionary<string, FieldVector>(fields));
    }

    private static QueryVector CreateQueryVector(string text, IReadOnlyDictionary<string, double> idf)
    {
        var terms = Tokenize(text).GroupBy(term => term, StringComparer.Ordinal).ToArray();
        var tokenCount = terms.Sum(group => group.Count());

        if (tokenCount == 0)
        {
            return QueryVector.Empty;
        }

        var weights = terms
            .Where(group => idf.ContainsKey(group.Key))
            .ToDictionary(
                group => group.Key,
                group => (group.Count() / (double)tokenCount) * idf[group.Key],
                StringComparer.Ordinal);
        var norm = Math.Sqrt(weights.Values.Sum(weight => weight * weight));
        return new QueryVector(new ReadOnlyDictionary<string, double>(weights), norm);
    }

    private double Score(IndexedDocument document, MinSearchQuery query, QueryVector queryVector)
    {
        var score = 0d;

        foreach (var field in options.TextFields)
        {
            var fieldVector = document.Fields[field];
            if (fieldVector.Norm == 0)
            {
                continue;
            }

            var dotProduct = queryVector.Weights.Sum(pair =>
                fieldVector.Weights.TryGetValue(pair.Key, out var fieldWeight) ? pair.Value * fieldWeight : 0d);
            var boost = query.FieldBoosts.GetValueOrDefault(field, 1d);
            score += boost * (dotProduct / (queryVector.Norm * fieldVector.Norm));
        }

        return score;
    }

    private void ValidateQuery(MinSearchQuery query)
    {
        foreach (var field in query.ExactMatchFilters.Keys)
        {
            if (!options.IsKeywordField(field))
            {
                throw new ArgumentException($"'{field}' is not a configured keyword field.", nameof(query));
            }
        }

        foreach (var field in query.FieldBoosts.Keys)
        {
            if (!options.IsTextField(field))
            {
                throw new ArgumentException($"'{field}' is not a configured text field.", nameof(query));
            }
        }
    }

    private static bool MatchesFilters(
        MinSearchDocument document,
        IReadOnlyDictionary<string, string> filters) =>
        filters.All(filter =>
            document.Fields.TryGetValue(filter.Key, out var value) &&
            StringComparer.Ordinal.Equals(value, filter.Value));

    private static IEnumerable<string> Tokenize(string text)
    {
        var buffer = new List<char>();

        foreach (var character in text)
        {
            if (char.IsLetterOrDigit(character))
            {
                buffer.Add(char.ToLowerInvariant(character));
            }
            else if (buffer.Count > 0)
            {
                yield return new string([.. buffer]);
                buffer.Clear();
            }
        }

        if (buffer.Count > 0)
        {
            yield return new string([.. buffer]);
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }

    private sealed record TokenizedDocument(
        MinSearchDocument Document,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, int>> Fields);

    private sealed record IndexedDocument(
        MinSearchDocument Document,
        IReadOnlyDictionary<string, FieldVector> Fields);

    private sealed record FieldVector(IReadOnlyDictionary<string, double> Weights, double Norm);

    private sealed record QueryVector(IReadOnlyDictionary<string, double> Weights, double Norm)
    {
/// <summary>Gets the zero-norm vector used when query tokenization yields no indexed terms.</summary>
        public static QueryVector Empty { get; } = new(new ReadOnlyDictionary<string, double>(new Dictionary<string, double>()), 0);
    }

    private sealed record IndexSnapshot(
        IReadOnlyList<IndexedDocument> Documents,
        IReadOnlyDictionary<string, double> InverseDocumentFrequency)
    {
/// <summary>Gets the empty immutable snapshot used before the first fit and after disposal.</summary>
        public static IndexSnapshot Empty { get; } = new(
            Array.Empty<IndexedDocument>(),
            new ReadOnlyDictionary<string, double>(new Dictionary<string, double>()));
    }
}
