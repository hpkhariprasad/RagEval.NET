# RagEval.NET

<!-- replace with your GitHub username -->
[![codecov](https://codecov.io/gh/YOUR_GITHUB_USERNAME/rageval-net/branch/main/graph/badge.svg)](https://codecov.io/gh/YOUR_GITHUB_USERNAME/rageval-net)

RagEval.NET is a .NET 8 library for evaluating the **output quality** of Retrieval-Augmented
Generation (RAG) pipelines. It uses an LLM-as-judge pattern — a language model scores a
question, an answer, and the retrieved context chunks against four RAG-specific metrics and
returns structured scores with reasoning.

RagEval.NET is an **evaluation layer**, not a pipeline builder. It doesn't retrieve documents,
call embedding models, or orchestrate prompts for you — it plugs in *after* your RAG pipeline
has produced an answer, and tells you how good that answer actually is.

## How this differs from other libraries

**vs. RAGSharp** — RAGSharp (and similar frameworks) helps you *build* a RAG pipeline:
chunking, embedding, retrieval, and generation. RagEval.NET does not build pipelines; it
*evaluates* whatever a pipeline (RAGSharp, Semantic Kernel, Kernel Memory, or a hand-rolled
implementation) produces. Use RAGSharp to build the pipeline, then use RagEval.NET to test it.

**vs. Semantic Kernel / Kernel Memory** — These are general-purpose orchestration and memory
frameworks that can be used to build RAG pipelines among other things. RagEval.NET has one job:
scoring RAG output quality. It has no opinion on how you built your pipeline.

**vs. Python RAGAS** — RAGAS pioneered LLM-as-judge scoring for RAG (faithfulness, answer
relevance, context precision/recall) in the Python ecosystem. RagEval.NET implements the same
core metrics natively for .NET, with first-class Azure OpenAI support, async/await throughout,
and a fluent builder API idiomatic to .NET — no Python interop required.

## Installation

```bash
dotnet add package RagEval.NET
```

## Quickstart

```csharp
using RagEval;
using RagEval.Models;

var evaluator = new RagEvaluatorBuilder()
    .UseAzureOpenAI(endpoint, apiKey, deploymentName)
    .WithJudgeModel("gpt-4o")
    .WithMaxConcurrency(5)
    .Build();

var input = new RagEvaluationInput
{
    Question    = "What is the notice period for termination?",
    Answer      = "The notice period is 30 days.",
    Contexts    = new[] { "...clause 12.1...", "...clause 12.2..." },
    GroundTruth = "30 days written notice per clause 12.1" // optional
};

RagEvaluationResult result = await evaluator.EvaluateAsync(input);

Console.WriteLine(result.Faithfulness);      // e.g. 1.0
Console.WriteLine(result.AnswerRelevance);   // e.g. 0.93
Console.WriteLine(result.ContextPrecision);  // e.g. 0.5
Console.WriteLine(result.ContextRecall);     // e.g. 1.0 (null if GroundTruth was omitted)
Console.WriteLine(result.Reasoning["Faithfulness"]);
```

## Metrics

| Metric | What it measures | Score interpretation | Requires GroundTruth |
|---|---|---|---|
| **Faithfulness** | Fraction of factual claims in the answer that are supported by the retrieved context. Catches hallucination. | 1.0 = every claim is grounded in context. 0.0 = the answer is entirely unsupported. | No |
| **AnswerRelevance** | Average similarity between the question and questions reverse-engineered from the answer. Catches off-topic or padded answers. | 1.0 = the answer is tightly focused on the question. Low scores indicate rambling or off-topic answers. | No |
| **ContextPrecision** | Fraction of retrieved context chunks that actually contributed to the answer. Catches noisy retrieval. | 1.0 = every retrieved chunk was useful. Low scores indicate the retriever is returning irrelevant chunks. | No |
| **ContextRecall** | Fraction of claims in a reference (ground truth) answer that are covered by the retrieved context. Catches incomplete retrieval. | 1.0 = the context contains everything needed to reproduce the ground truth. `null` if no `GroundTruth` was supplied. | Yes |

Each score is a `double?` in the range `0.0`–`1.0`, or `null` if the judge's response could not
be parsed or the metric could not be evaluated (e.g. `ContextRecall` with no `GroundTruth`). The
`Reasoning` dictionary on `RagEvaluationResult` explains how each score was derived, keyed by
metric name (`"Faithfulness"`, `"AnswerRelevance"`, `"ContextPrecision"`, `"ContextRecall"`).

## Azure OpenAI setup

1. Create an Azure OpenAI resource and deploy a chat-completion model (e.g. `gpt-4o`).
2. Note your resource **endpoint** (e.g. `https://my-resource.openai.azure.com/`), **API key**,
   and **deployment name** from the Azure Portal.
3. Configure the evaluator:

   ```csharp
   var evaluator = new RagEvaluatorBuilder()
       .UseAzureOpenAI(
           endpoint: Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")!,
           apiKey: Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")!,
           deploymentName: Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")!)
       .Build();
   ```

   `WithJudgeModel` is optional metadata — since an Azure deployment name can differ from the
   underlying model name, it lets you record which model actually backs the deployment for your
   own diagnostics (exposed via `evaluator.JudgeModel`).

Prefer plain OpenAI instead? Use `.UseOpenAI(apiKey, model)` in place of `.UseAzureOpenAI(...)`.

## Batch evaluation

```csharp
var inputs = new List<RagEvaluationInput> { input1, input2, input3 };

IReadOnlyList<RagEvaluationResult> results = await evaluator.EvaluateBatchAsync(inputs);

RagEvaluationSummary summary = results.GetSummary();

Console.WriteLine($"Avg Faithfulness: {summary.AvgFaithfulness:F2}");
Console.WriteLine($"Avg Answer Relevance: {summary.AvgAnswerRelevance:F2}");
Console.WriteLine($"Avg Context Precision: {summary.AvgContextPrecision:F2}");
Console.WriteLine($"Avg Context Recall: {summary.AvgContextRecall:F2}");
Console.WriteLine($"Total evaluated: {summary.TotalEvaluated}");
```

`WithMaxConcurrency(n)` bounds how many judge calls run concurrently during batch evaluation,
so you can stay within your Azure OpenAI or OpenAI rate limits.

See [`samples/RagEval.Samples/BasicEvaluation.cs`](samples/RagEval.Samples/BasicEvaluation.cs)
for a full runnable example.

## Export results

Batch results can be written to disk as JSON or CSV via the `ExportAsync` extension method on
`IReadOnlyList<RagEvaluationResult>`. The destination directory is created automatically if it
doesn't already exist.

```csharp
using RagEval.Export;

// JSON (default) — a pretty-printed array of full result objects, including reasoning.
await results.ExportAsync("output/results.json");

// CSV — one row per result, with a fixed set of columns for spreadsheet analysis.
await results.ExportAsync("output/results.csv", RagEvalExportFormat.Csv);
```

The CSV format has the header row:

```
Question,Answer,Faithfulness,AnswerRelevance,ContextPrecision,ContextRecall,FaithfulnessReasoning,AnswerRelevanceReasoning,ContextPrecisionReasoning,ContextRecallReasoning
```

Null scores (e.g. `ContextRecall` when no `GroundTruth` was supplied) are exported as an empty
field in CSV and as JSON `null` in the JSON format. To export directly with a specific format,
use `JsonRagEvalExporter` or `CsvRagEvalExporter` (both implement `IRagEvalExporter`) instead of
the extension method.

## Contributing

Contributions are welcome.

1. Fork the repo and create a feature branch.
2. Build the solution: `dotnet build RagEvalNet.sln`.
3. Run the tests: `dotnet test`.
4. Keep the coding standards used throughout the codebase: nullable reference types enabled,
   async/await everywhere (no sync-over-async), `CancellationToken` on every public async
   method, XML doc comments on public members, and all judge-facing prompt strings defined as
   constants in `Prompts/MetricPrompts.cs`.
5. Add or update tests for any behavioral change — metric evaluators are tested against a
   mocked `ILlmJudge`, so no real API calls are needed to run the test suite.
6. Open a pull request describing the change and why it's needed.

## CI Setup

CI runs tests with code coverage and uploads results to Codecov; to enable this on a fork, generate a repository upload token at [codecov.io](https://codecov.io) (after linking the repo there) and add it as a `CODECOV_TOKEN` secret under the repo's GitHub Settings → Secrets and variables → Actions.

## License

MIT
