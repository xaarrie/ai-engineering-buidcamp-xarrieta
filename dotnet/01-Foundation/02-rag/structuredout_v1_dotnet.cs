#:sdk Microsoft.NET.Sdk
#:package Microsoft.Agents.AI.OpenAI@1.22.0

// Lesson guide: this runnable script keeps the package directives, environment configuration, and framework agent boundary visible. The local records/classes describe input and output contracts; AsAIAgent constructs the agent and RunAsync or RunStreamingAsync is the deliberate model boundary. Offline fixtures, where present, exercise deterministic local behavior without live model calls. The lesson is intentionally scoped to its named concept rather than production retries, semantic embeddings, or transport policy.

using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001 // The OpenAI Responses client is marked experimental by the OpenAI SDK.

LoadRepositoryEnvironment();

var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY")
    ?? throw new InvalidOperationException("OPENAI_API_KEY is not configured in the repository .env file.");
var model = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL_NAME") ?? "gpt-4o-mini";

// The type is the contract. Its required properties become the structured-output schema.
var eventPrompt = "Alice and Bob are going to a science fair on Friday.";
OpenAIClient client = new(apiKey);
AIAgent calendarAgent = client.GetResponsesClient().AsAIAgent(
    model: model,
    name: "CalendarEventExtractor",
    instructions: "Extract calendar event information from the user's text.");

AgentResponse<CalendarEvent> structuredResponse = await calendarAgent.RunAsync<CalendarEvent>(eventPrompt);

Console.WriteLine("Structured output:");
Console.WriteLine($"Name: {structuredResponse.Result.Name}");
Console.WriteLine($"Date: {structuredResponse.Result.Date}");
Console.WriteLine($"Participants: {string.Join(", ", structuredResponse.Result.Participants)}");
Console.WriteLine("Raw JSON response:");
Console.WriteLine(structuredResponse.Text);

// Without RunAsync<T>, the same intent is only prose. Its layout is not a typed contract.
AIAgent textAgent = client.GetResponsesClient().AsAIAgent(
    model: model,
    name: "CalendarEventTextResponder",
    instructions: "Describe the calendar event in a friendly, human-readable sentence.");
AgentResponse unstructuredResponse = await textAgent.RunAsync(eventPrompt);

Console.WriteLine("\nUnstructured text:");
Console.WriteLine(unstructuredResponse.Text);
Console.WriteLine("Unstructured text has no CalendarEvent result to validate or consume by property.");

#pragma warning restore OPENAI001

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

// CalendarEvent is the lesson's explicit data or orchestration boundary; keeping it local makes the phase being taught visible.
public sealed class CalendarEvent
{
// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("name")]
    public required string Name { get; init; }

// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("date")]
    public required string Date { get; init; }

// This property is a serialized contract field; its name is intentionally stable for the model response boundary.
    [JsonPropertyName("participants")]
    public required IReadOnlyList<string> Participants { get; init; }
}
