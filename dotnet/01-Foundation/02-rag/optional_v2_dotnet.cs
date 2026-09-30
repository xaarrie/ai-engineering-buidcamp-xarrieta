#:sdk Microsoft.NET.Sdk
#:package Microsoft.Agents.AI.OpenAI@1.22.0

// Lesson guide: this runnable script keeps the package directives, environment configuration, and framework agent boundary visible. The local records/classes describe input and output contracts; AsAIAgent constructs the agent and RunAsync or RunStreamingAsync is the deliberate model boundary. Offline fixtures, where present, exercise deterministic local behavior without live model calls. The lesson is intentionally scoped to its named concept rather than production retries, semantic embeddings, or transport policy.

using System.Text.Json.Serialization;
using Microsoft.Agents.AI;
using OpenAI;
using OpenAI.Responses;

#pragma warning disable OPENAI001 // The Responses client is experimental in the OpenAI SDK.

if (args.SequenceEqual(["--offline-fixture"]))
{
    RunOfflineFixture();
    return;
}

LoadRepositoryEnvironment();
IReadOnlyList<EvaluationCase> cases = CreateDataset();
Console.WriteLine("This lesson runs reports and dashboard-style summaries locally. Only Evidently Cloud upload and dashboard hosting are excluded.");

// Descriptors make each derived value explicit and repeatable for every dataset row.
IReadOnlyList<DatasetDescriptor> descriptors =
[
    new("New response length", evaluation => TextLength(evaluation.Case.NewResponse).ToString()),
    new("Reference word difference", evaluation => WordDifference(evaluation.Case.ReferenceResponse, evaluation.Case.NewResponse).ToString())
];
IReadOnlyList<QualityTest> deterministicTests =
[
    new("Response is not empty", evaluation => !string.IsNullOrWhiteSpace(evaluation.Case.NewResponse)),
    new("New response stays close to the reference", evaluation => WordDifference(evaluation.Case.ReferenceResponse, evaluation.Case.NewResponse) <= 8)
];
IReadOnlyList<CaseEvaluation> evaluations = EvaluateDataset(cases, descriptors, deterministicTests);
RegressionSummary regression = CompareResponses(cases);
LocalEvaluationReport report = BuildReport(evaluations, regression);
PrintReport(report);
PrintDashboard(BuildDashboard(report));

string? apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    Console.WriteLine("OPENAI_API_KEY is not configured, so the optional MAF LLM-as-a-judge quality criterion was skipped.");
    return;
}

string model = Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL_NAME") ?? "gpt-4o-mini";
OpenAIClient client = new(apiKey);
AIAgent judge = client.GetResponsesClient().AsAIAgent(
    model: model,
    name: "ResponseQualityJudge",
    instructions: "Judge whether NEW_RESPONSE is at least as correct and concise as OLD_RESPONSE for QUESTION. Return the requested schema.");

var judgeResults = new Dictionary<string, QualityJudgment>(StringComparer.Ordinal);
foreach (EvaluationCase item in cases)
{
    AgentResponse<QualityJudgment> judgment = await judge.RunAsync<QualityJudgment>(
        $"QUESTION:\n{item.Question}\n\nOLD_RESPONSE:\n{item.OldResponse}\n\nNEW_RESPONSE:\n{item.NewResponse}");
    judgeResults[item.Question] = judgment.Result;
}

// The optional LLM criterion joins the local report rather than requiring a hosted service.
LocalEvaluationReport judgedReport = BuildReport(evaluations, regression, judgeResults);
PrintReport(judgedReport);
PrintDashboard(BuildDashboard(judgedReport));

#pragma warning restore OPENAI001

static IReadOnlyList<EvaluationCase> CreateDataset() =>
[
    new("Why is the sky blue?", "Air molecules scatter blue light more than red light.", "The sky appears blue because air molecules scatter blue sunlight strongly."),
    new("How do I reset a password?", "Use Forgot Password on the login page.", "Use Forgot Password on the login page, then follow the email instructions."),
    new("Can an account have multiple users?", "No. The service supports one user per account.", "Yes. Open Manage Users and add another person.")
];

// Text length is deliberately deterministic: characters after trimming whitespace.
static int TextLength(string response) => response.Trim().Length;

static IReadOnlyList<CaseEvaluation> EvaluateDataset(
    IEnumerable<EvaluationCase> cases,
    IReadOnlyList<DatasetDescriptor> descriptors,
    IReadOnlyList<QualityTest> tests) =>
    cases.Select(item =>
    {
        var pending = new CaseEvaluation(item, new Dictionary<string, string>(), new Dictionary<string, bool>());
        IReadOnlyDictionary<string, string> values = descriptors.ToDictionary(descriptor => descriptor.Name, descriptor => descriptor.Compute(pending));
        var evaluated = pending with { DescriptorValues = values };
        return evaluated with { TestResults = tests.ToDictionary(test => test.Name, test => test.Passes(evaluated)) };
    }).ToList();

static LocalEvaluationReport BuildReport(
    IReadOnlyList<CaseEvaluation> evaluations,
    RegressionSummary regression,
    IReadOnlyDictionary<string, QualityJudgment>? judgeResults = null)
{
    int deterministicPassed = evaluations.Sum(evaluation => evaluation.TestResults.Count(result => result.Value));
    int deterministicTotal = evaluations.Sum(evaluation => evaluation.TestResults.Count);
    int judgePassed = judgeResults?.Values.Count(result => result.Passes) ?? 0;
    int judgeTotal = judgeResults?.Count ?? 0;
    return new LocalEvaluationReport(evaluations, regression, deterministicPassed, deterministicTotal, judgePassed, judgeTotal);
}

static DashboardSummary BuildDashboard(LocalEvaluationReport report) => new(
    report.Evaluations.Count,
    report.DeterministicPassed,
    report.DeterministicTotal,
    report.Regression,
    report.JudgePassed,
    report.JudgeTotal);

static void PrintReport(LocalEvaluationReport report)
{
    Console.WriteLine("\nLOCAL EVALUATION REPORT");
    foreach (CaseEvaluation evaluation in report.Evaluations)
    {
        string descriptors = string.Join(", ", evaluation.DescriptorValues.Select(pair => $"{pair.Key}={pair.Value}"));
        string tests = string.Join(", ", evaluation.TestResults.Select(pair => $"{pair.Key}={(pair.Value ? "pass" : "fail")}"));
        Console.WriteLine($"- {evaluation.Case.Question}\n  descriptors: {descriptors}\n  tests: {tests}");
    }
    Console.WriteLine($"Deterministic tests: {report.DeterministicPassed}/{report.DeterministicTotal} passed.");
    Console.WriteLine($"Optional LLM judge: {report.JudgePassed}/{report.JudgeTotal} passed.");
    Console.WriteLine($"Regression: {report.Regression.Improved} improved, {report.Regression.Regressed} regressed, {report.Regression.Unchanged} unchanged.");
}

static void PrintDashboard(DashboardSummary dashboard) =>
    Console.WriteLine($"LOCAL DASHBOARD | rows={dashboard.RowCount} | tests={dashboard.DeterministicPassed}/{dashboard.DeterministicTotal} | regression: +{dashboard.Regression.Improved}/-{dashboard.Regression.Regressed}/={dashboard.Regression.Unchanged} | LLM judge={dashboard.JudgePassed}/{dashboard.JudgeTotal}");

static RegressionSummary CompareResponses(IEnumerable<EvaluationCase> cases)
{
    int improved = 0, regressed = 0, unchanged = 0;
    foreach (EvaluationCase item in cases)
    {
        int oldDistance = WordDifference(item.ReferenceResponse, item.OldResponse);
        int newDistance = WordDifference(item.ReferenceResponse, item.NewResponse);
        if (newDistance < oldDistance) improved++;
        else if (newDistance > oldDistance) regressed++;
        else unchanged++;
    }
    return new RegressionSummary(improved, regressed, unchanged);
}

// A deterministic stand-in for a reference-based regression criterion: lower distance is better.
static int WordDifference(string reference, string candidate)
{
    HashSet<string> expected = Tokenize(reference);
    HashSet<string> actual = Tokenize(candidate);
    expected.SymmetricExceptWith(actual);
    return expected.Count;
}
static HashSet<string> Tokenize(string value) => value.ToLowerInvariant().Split([' ', '.', ',', '?', '!'], StringSplitOptions.RemoveEmptyEntries).ToHashSet(StringComparer.Ordinal);

static void RunOfflineFixture()
{
    EvaluationCase[] fixture =
    [
        new("q1", "blue light scatters", "blue light scatters", "blue light scatters"),
        new("q2", "one user only", "one user only", "add many users")
    ];
    IReadOnlyList<DatasetDescriptor> descriptors = [new("New response length", item => TextLength(item.Case.NewResponse).ToString())];
    IReadOnlyList<QualityTest> tests = [new("Response is not empty", item => !string.IsNullOrWhiteSpace(item.Case.NewResponse))];
    IReadOnlyList<CaseEvaluation> evaluations = EvaluateDataset(fixture, descriptors, tests);
    RegressionSummary regression = CompareResponses(fixture);
    LocalEvaluationReport report = BuildReport(evaluations, regression);
    DashboardSummary dashboard = BuildDashboard(report);
    if (TextLength("  hello ") != 5 || report.DeterministicPassed != 2 || report.DeterministicTotal != 2 ||
        regression != new RegressionSummary(0, 1, 1) || dashboard.RowCount != 2 || dashboard.Regression.Regressed != 1)
        throw new InvalidOperationException("Offline report, test, regression, or dashboard fixture failed.");
    Console.WriteLine("SUCCESS: offline descriptors, tests, report, regression, and dashboard summary fixture passed.");
}

static void LoadRepositoryEnvironment()
{
    string file = Path.Combine(FindRepositoryRoot(), ".env");
    if (!File.Exists(file)) return;
    foreach (string line in File.ReadLines(file))
    {
        string text = line.Trim(); int equals = text.IndexOf('=');
        if (text.Length > 0 && !text.StartsWith('#') && equals > 0 && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(text[..equals].Trim())))
            Environment.SetEnvironmentVariable(text[..equals].Trim(), text[(equals + 1)..].Trim().Trim('"', '\''));
    }
}
static string FindRepositoryRoot()
{
    for (DirectoryInfo? directory = new(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
        if (Directory.Exists(Path.Combine(directory.FullName, ".git")) || File.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName;
    throw new DirectoryNotFoundException("Could not find the repository root.");
}

// One evaluation row ties the question to its reference, prior answer, and candidate answer for every comparison.
sealed record EvaluationCase(string Question, string ReferenceResponse, string OldResponse, string NewResponse)
{
    // This convenience overload treats the reference as the old response when no separate baseline is supplied.
    public EvaluationCase(string question, string referenceResponse, string newResponse) : this(question, referenceResponse, referenceResponse, newResponse) { }
}
// A descriptor names a derived measurement and provides the function that computes its printable value.
sealed record DatasetDescriptor(string Name, Func<CaseEvaluation, string> Compute);
// A quality test names a pass/fail rule; its function can inspect the case and already computed descriptors.
sealed record QualityTest(string Name, Func<CaseEvaluation, bool> Passes);
// Evaluation keeps descriptor values and boolean outcomes together so report generation reuses, rather than recomputes, criteria.
sealed record CaseEvaluation(EvaluationCase Case, IReadOnlyDictionary<string, string> DescriptorValues, IReadOnlyDictionary<string, bool> TestResults);
// These counts classify candidate responses by their distance from the reference: closer, farther, or unchanged.
sealed record RegressionSummary(int Improved, int Regressed, int Unchanged);
// The report combines per-row evidence with totals for deterministic tests and the optional model judge.
sealed record LocalEvaluationReport(IReadOnlyList<CaseEvaluation> Evaluations, RegressionSummary Regression, int DeterministicPassed, int DeterministicTotal, int JudgePassed, int JudgeTotal);
// The dashboard projection contains only the aggregate fields needed by the one-line summary.
sealed record DashboardSummary(int RowCount, int DeterministicPassed, int DeterministicTotal, RegressionSummary Regression, int JudgePassed, int JudgeTotal);
// The typed judge response supplies the optional model-backed criterion that is added to the local report.
public sealed class QualityJudgment
{
    // True marks the candidate response as meeting the judge's correctness and concision criterion.
    [JsonPropertyName("passes")] public required bool Passes { get; init; }
    // The judge's rationale is retained beside the boolean result for human inspection.
    [JsonPropertyName("reason")] public required string Reason { get; init; }
}
