#:sdk Microsoft.NET.Sdk
#:package Microsoft.Agents.AI.OpenAI@1.22.0

// Lesson guide: Microsoft Agent Framework owns the model/tool-calling loop. The
// local search function is deterministic so this lesson can be verified offline;
// the live path demonstrates the same tool attached to a session-aware agent.

using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;

if (args is ["--verify"])
{
    VerifyOfflineTool();
    return;
}

LoadRepositoryEnvironment();

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY is not configured in the repository .env file.");
var model = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL_NAME") ?? "gpt-4o-mini";

OpenAIClient client = new(apiKey);
AIFunction searchTool = AIFunctionFactory.Create(
    SearchKnowledgeBase,
    name: "search_knowledge_base",
    description: "Search the local documentation knowledge base. Use this before answering documentation questions; never invent facts when the search has no result.");

// ChatClientAgent receives the tool as an AITool and delegates the agentic
// function-call loop to Microsoft Agent Framework rather than implementing it here.
ChatClientAgent agent = new(
    chatClient: client.GetChatClient(model).AsIChatClient(),
    name: "DocumentationAssistant",
    instructions: "You are a documentation assistant. When a question needs knowledge-base facts, call search_knowledge_base before answering and call it again when more facts are needed. Use only returned facts, and clearly say when no result was found.",
    tools: [searchTool]);

Console.WriteLine("Session-aware agentic loop:");
AgentSession session = await agent.CreateSessionAsync();
AgentResponse firstResponse = await agent.RunAsync(
    "How do I create a dashboard, what can its panels display, and when should I use alerts? Search the knowledge base as many times as needed before answering.",
    session);
Console.WriteLine(firstResponse.Text);

AgentResponse followUpResponse = await agent.RunAsync(
    "What should I do if the knowledge base has no matching article?",
    session);
Console.WriteLine(followUpResponse.Text);

static string SearchKnowledgeBase(string query, int limit = 3)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(query);
    if (limit is < 1 or > 5)
    {
        throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 5.");
    }

    var normalizedQuery = query.Trim();
    var matches = KnowledgeBaseData.Articles
        .Where(article => normalizedQuery.Contains(article.Keyword, StringComparison.OrdinalIgnoreCase))
        .Take(limit)
        .ToArray();

    if (matches.Length == 0)
    {
        return $"No knowledge base results found for query '{normalizedQuery}'.";
    }

    return string.Join(
        Environment.NewLine,
        matches.Select(article => $"[{article.Title}] {article.Content}"));
}

static void VerifyOfflineTool()
{
    const string expectedMatch = "[Dashboards] Create a dashboard from the Dashboards page, then add panels to it.";
    string match = SearchKnowledgeBase("dashboard", 1);
    if (!string.Equals(match, expectedMatch, StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected deterministic search result, got: {match}");
    }

    string noResult = SearchKnowledgeBase("unlisted topic");
    if (!noResult.StartsWith("No knowledge base results found", StringComparison.Ordinal))
    {
        throw new InvalidOperationException($"Expected explicit no-result behavior, got: {noResult}");
    }

    AssertThrows<ArgumentException>(() => SearchKnowledgeBase("  "));
    AssertThrows<ArgumentOutOfRangeException>(() => SearchKnowledgeBase("dashboard", 0));

    AIFunction registeredTool = AIFunctionFactory.Create(
        SearchKnowledgeBase,
        name: "search_knowledge_base",
        description: "Offline registration check for the deterministic search function.");
    if (!string.Equals(registeredTool.Name, "search_knowledge_base", StringComparison.Ordinal))
    {
        throw new InvalidOperationException("The search function was not registered with the expected tool name.");
    }

    Console.WriteLine("Offline verification passed: match, no-result, argument validation, and tool registration.");
}

static void AssertThrows<TException>(Action action)
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

static void LoadRepositoryEnvironment()
{
    string repositoryRoot = FindRepositoryRoot();
    string environmentFile = Path.Combine(repositoryRoot, ".env");
    if (!File.Exists(environmentFile))
    {
        return;
    }

    foreach (string line in File.ReadLines(environmentFile))
    {
        string trimmedLine = line.Trim();
        if (trimmedLine.Length == 0 || trimmedLine.StartsWith('#'))
        {
            continue;
        }

        int separator = trimmedLine.IndexOf('=');
        if (separator <= 0)
        {
            continue;
        }

        string key = trimmedLine[..separator].Trim();
        string value = trimmedLine[(separator + 1)..].Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(key)))
        {
            Environment.SetEnvironmentVariable(key, value);
        }
    }
}

static string FindRepositoryRoot()
{
    for (DirectoryInfo? directory = new(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
    {
        string gitMarker = Path.Combine(directory.FullName, ".git");
        if (Directory.Exists(gitMarker) || File.Exists(gitMarker))
        {
            return directory.FullName;
        }
    }

    throw new DirectoryNotFoundException("Could not find the repository root needed to load .env.");
}

file sealed record KnowledgeArticle(string Keyword, string Title, string Content);

file static class KnowledgeBaseData
{
    public static readonly KnowledgeArticle[] Articles =
    [
        new("dashboard", "Dashboards", "Create a dashboard from the Dashboards page, then add panels to it."),
        new("panel", "Panels", "Panels display metrics and charts inside a dashboard."),
        new("alert", "Alerts", "Alerts notify you when a monitored metric crosses its configured threshold."),
    ];
}
