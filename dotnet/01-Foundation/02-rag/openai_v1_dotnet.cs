#:sdk Microsoft.NET.Sdk
#:package Microsoft.Agents.AI.OpenAI@1.22.0

// Lesson guide: this runnable script keeps the package directives, environment configuration, and framework agent boundary visible. The local records/classes describe input and output contracts; AsAIAgent constructs the agent and RunAsync or RunStreamingAsync is the deliberate model boundary. Offline fixtures, where present, exercise deterministic local behavior without live model calls. The lesson is intentionally scoped to its named concept rather than production retries, semantic embeddings, or transport policy.

using System.Globalization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001 // The OpenAI Responses client is marked experimental by the OpenAI SDK.

LoadRepositoryEnvironment();

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY is not configured in the repository .env file.");
var model = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL_NAME") ?? "gpt-4o-mini";

OpenAIClient client = new(apiKey);

// The Responses API is the recommended OpenAI integration for new Agent Framework applications.
AIAgent basicAgent = client.GetResponsesClient().AsAIAgent(
    model: model,
    name: "LessonAssistant");

// Basic request
Console.WriteLine("Basic request:");
AgentResponse basicResponse = await basicAgent.RunAsync("Tell me a joke about dogs.");
Console.WriteLine(basicResponse.Text);
PrintResponseDetails(basicResponse, model);

// Message request
Console.WriteLine("\nMessage request:");
var catMessages = new[]
{
    new ChatMessage(ChatRole.User, "Tell me a joke about cats."),
};
AgentResponse messageResponse = await basicAgent.RunAsync(catMessages);
Console.WriteLine(messageResponse.Text);
PrintResponseDetails(messageResponse, model);

// Streaming responses
Console.WriteLine("\nStreaming response:");
AgentResponse streamingResponse = await StreamToConsole(
    basicAgent.RunStreamingAsync("Tell me a short bad-time story about a unicorn."))
    .ToAgentResponseAsync();
Console.WriteLine();
PrintResponseDetails(streamingResponse, model);

// Instructions influence every request made through this agent.
const string instructions = "Always add relevant emojis to your answer.";
AIAgent instructedAgent = client.GetResponsesClient().AsAIAgent(
    model: model,
    name: "EmojiAssistant",
    instructions: instructions);

Console.WriteLine("\nInstructions:");
AgentResponse instructedResponse = await instructedAgent.RunAsync("Tell me a short bad-time story about a unicorn.");
Console.WriteLine(instructedResponse.Text);

// A call without a session is stateless: this request does not know a previous name.
Console.WriteLine("\nStateless request:");
AgentResponse statelessResponse = await instructedAgent.RunAsync("What is my name?");
Console.WriteLine(statelessResponse.Text);

// A session keeps the conversation history for subsequent calls.
Console.WriteLine("\nStateful session:");
AgentSession session = await instructedAgent.CreateSessionAsync();
AgentResponse introductionResponse = await instructedAgent.RunAsync("My name is Alexey.", session);
Console.WriteLine(introductionResponse.Text);
AgentResponse rememberedNameResponse = await instructedAgent.RunAsync("What is my name?", session);
Console.WriteLine(rememberedNameResponse.Text);

#pragma warning restore OPENAI001

static void PrintResponseDetails(AgentResponse response, string model)
{
    Console.WriteLine($"Model requested: {model}");
    Console.WriteLine($"Response messages: {response.Messages.Count}");
    Console.WriteLine($"Raw provider response metadata type: {response.RawRepresentation?.GetType().FullName ?? "Not available"}");

    if (response.Usage is not { } usage)
    {
        Console.WriteLine("Usage metadata is not available from this provider response.");
        return;
    }

    var inputTokens = usage.InputTokenCount ?? 0;
    var outputTokens = usage.OutputTokenCount ?? 0;
    Console.WriteLine($"Input tokens: {inputTokens}");
    Console.WriteLine($"Output tokens: {outputTokens}");
    Console.WriteLine($"Total tokens: {inputTokens + outputTokens}");

    if (TryCalculateCost(model, inputTokens, outputTokens, out decimal cost))
    {
        Console.WriteLine($"Illustrative cost: ${cost.ToString("F6", CultureInfo.InvariantCulture)}");
    }
    else
    {
        Console.WriteLine("Cost is unavailable because this model has no configured lesson price.");
    }
}

static async IAsyncEnumerable<AgentResponseUpdate> StreamToConsole(
    IAsyncEnumerable<AgentResponseUpdate> updates)
{
    await foreach (AgentResponseUpdate update in updates)
    {
        Console.Write(update.Text);
        yield return update;
    }
}

static bool TryCalculateCost(string model, long inputTokens, long outputTokens, out decimal cost)
{
    // Prices are USD per million tokens and are kept here only for the lesson calculation.
    var prices = new Dictionary<string, (decimal Input, decimal Output)>(StringComparer.OrdinalIgnoreCase)
    {
        ["gpt-4o-mini"] = (0.15m, 0.60m),
        ["gpt-5-nano"] = (0.075m, 0.30m),
        ["gpt-5-mini"] = (0.25m, 2.00m),
        ["gpt-5.2"] = (1.75m, 14.00m),
        ["gpt-5.2-pro"] = (21.00m, 168.00m),
    };

    if (!prices.TryGetValue(model, out var price))
    {
        cost = 0;
        return false;
    }

    cost = (inputTokens / 1_000_000m * price.Input) + (outputTokens / 1_000_000m * price.Output);
    return true;
}

static void LoadRepositoryEnvironment()
{
    var repositoryRoot = FindRepositoryRoot();
    var environmentFile = Path.Combine(repositoryRoot, ".env");

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
