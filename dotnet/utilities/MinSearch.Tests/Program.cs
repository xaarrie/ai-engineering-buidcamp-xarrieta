// Fixture lesson: these assertions protect immutable public contracts, Fit versus Append semantics, exact keyword filters, positive field boosts, TF-IDF ranking, and ordinal tie-breaking without a search service.

using System.Collections;
using MinSearch;

var tests = new (string Name, Func<Task> Execute)[]
{
    ("fit replaces the index", FitReplacesIndexAsync),
    ("append extends the index", AppendExtendsIndexAsync),
    ("field boosts affect ranking", FieldBoostsAffectRankingAsync),
    ("keyword filters select exact matches", KeywordFiltersSelectExactMatchesAsync),
    ("result limits constrain responses", ResultLimitsConstrainResponsesAsync),
    ("equal scores use deterministic document-id ordering", EqualScoresUseDeterministicOrderingAsync),
    ("empty and unmatched searches return no hits", EmptyAndUnmatchedSearchesReturnNoHitsAsync),
    ("public contracts are immutable", PublicContractsAreImmutableAsync)
};

try
{
    foreach (var test in tests)
    {
        await test.Execute();
        Console.WriteLine($"PASS: {test.Name}");
    }

    Console.WriteLine($"SUCCESS: {tests.Length} MinSearch assertions passed.");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"FAIL: {exception.Message}");
    return 1;
}

static InMemoryMinSearchIndex CreateIndex() => new(
    new MinSearchIndexOptions(["title", "content"], ["category"]));

static MinSearchDocument Document(string id, string title, string content, string category) => new(
    id,
    new Dictionary<string, string>
    {
        ["title"] = title,
        ["content"] = content,
        ["category"] = category
    });

static async Task FitReplacesIndexAsync()
{
    using var index = CreateIndex();
    await index.FitAsync([Document("old", "Legacy guide", "obsolete notes", "guide")]);
    await index.FitAsync([Document("new", "Current guide", "current Azure instructions", "guide")]);

    var response = await index.SearchAsync(new MinSearchQuery("legacy current", resultLimit: 10));
    Equal(1, response.Hits.Count, "FitAsync must replace the previous document set.");
    Equal("new", response.Hits[0].Document.Id, "The replacement document must remain searchable.");
}

static async Task AppendExtendsIndexAsync()
{
    using var index = CreateIndex();
    await index.FitAsync([Document("first", "Azure setup", "setup instructions", "guide")]);
    await index.AppendAsync([Document("second", "Azure deployment", "deployment instructions", "guide")]);

    var response = await index.SearchAsync(new MinSearchQuery("Azure", resultLimit: 10));
    Equal(2, response.Hits.Count, "AppendAsync must retain existing documents and add new documents.");
}

static async Task FieldBoostsAffectRankingAsync()
{
    using var index = CreateIndex();
    await index.FitAsync(
    [
        Document("title-match", "Azure migration", "general notes", "guide"),
        Document("content-match", "General notes", "Azure migration", "guide")
    ]);

    var response = await index.SearchAsync(new MinSearchQuery(
        "Azure migration",
        fieldBoosts: new Dictionary<string, double> { ["title"] = 3d },
        resultLimit: 10));

    Equal("title-match", response.Hits[0].Document.Id, "A higher title boost must rank the title match first.");
}

static async Task KeywordFiltersSelectExactMatchesAsync()
{
    using var index = CreateIndex();
    await index.FitAsync(
    [
        Document("guide", "Azure guide", "migration steps", "guide"),
        Document("reference", "Azure reference", "migration steps", "reference")
    ]);

    var response = await index.SearchAsync(new MinSearchQuery(
        "Azure",
        exactMatchFilters: new Dictionary<string, string> { ["category"] = "guide" },
        resultLimit: 10));

    Equal(1, response.Hits.Count, "Exact keyword filters must exclude non-matching documents.");
    Equal("guide", response.Hits[0].Document.Id, "The exact keyword match must be returned.");
}

static async Task ResultLimitsConstrainResponsesAsync()
{
    using var index = CreateIndex();
    await index.FitAsync(
    [
        Document("a", "Azure a", "migration", "guide"),
        Document("b", "Azure b", "migration", "guide"),
        Document("c", "Azure c", "migration", "guide")
    ]);

    var response = await index.SearchAsync(new MinSearchQuery("Azure", resultLimit: 1));
    Equal(1, response.Hits.Count, "ResultLimit must cap the returned hit count.");
}

static async Task EqualScoresUseDeterministicOrderingAsync()
{
    using var index = CreateIndex();
    await index.FitAsync(
    [
        Document("z-last", "Azure guide", "migration", "guide"),
        Document("a-first", "Azure guide", "migration", "guide")
    ]);

    var response = await index.SearchAsync(new MinSearchQuery("Azure", resultLimit: 10));
    Equal("a-first", response.Hits[0].Document.Id, "Equal scores must be ordered by ordinal document id.");
}

static async Task EmptyAndUnmatchedSearchesReturnNoHitsAsync()
{
    using var index = CreateIndex();
    Equal(0, (await index.SearchAsync(new MinSearchQuery("Azure"))).Hits.Count, "An empty index must return no hits.");

    await index.FitAsync([Document("only", "Azure guide", "migration", "guide")]);
    Equal(0, (await index.SearchAsync(new MinSearchQuery("Kubernetes"))).Hits.Count, "An unmatched query must return no hits.");
    Equal(0, (await index.SearchAsync(new MinSearchQuery(string.Empty))).Hits.Count, "An empty query must return no hits.");
}

static Task PublicContractsAreImmutableAsync()
{
    var fields = new Dictionary<string, string> { ["title"] = "Original" };
    var document = new MinSearchDocument("immutable", fields);
    fields["title"] = "Changed";
    Equal("Original", document.Fields["title"], "Document fields must be copied at construction.");
    Throws<NotSupportedException>(() => ((IDictionary)document.Fields).Add("content", "cannot mutate"));

    var textFields = new List<string> { "title" };
    var options = new MinSearchIndexOptions(textFields);
    textFields[0] = "content";
    Equal("title", options.TextFields[0], "Index options must copy field lists at construction.");
    Throws<NotSupportedException>(() => ((IList)options.TextFields).Add("content"));

    var filters = new Dictionary<string, string> { ["category"] = "guide" };
    var query = new MinSearchQuery("Azure", filters);
    filters["category"] = "reference";
    Equal("guide", query.ExactMatchFilters["category"], "Queries must copy filters at construction.");
    Throws<NotSupportedException>(() => ((IDictionary)query.ExactMatchFilters).Add("other", "value"));

    var response = new MinSearchResponse([new MinSearchHit(document, 1)]);
    Throws<NotSupportedException>(() => ((IList)response.Hits).Add(new MinSearchHit(document, 2)));
    return Task.CompletedTask;
}

static void Equal<T>(T expected, T actual, string message)
    where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{message} Expected '{expected}', got '{actual}'.");
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
