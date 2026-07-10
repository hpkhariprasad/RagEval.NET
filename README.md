# RagEval.NET


[![codecov](https://codecov.io/gh/hpkhariprasad/rageval-net/branch/main/graph/badge.svg)](https://codecov.io/gh/hpkhariprasad/rageval-net)

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

## How It Compares to Python RAGAS

| Feature                  | RagEval.NET  | Python RAGAS |
|--------------------------|--------------|--------------|
| Language                 | .NET 8 (C#)  | Python       |
| Azure OpenAI native      | ✅           | Partial      |
| Semantic Kernel fit      | ✅           | ❌           |
| M.Extensions.AI support  | ✅           | ❌           |
| LLM-as-judge             | ✅           | ✅           |
| Metrics (core 4)         | ✅           | ✅           |
| CI threshold assertions  | ✅           | ❌           |
| JSON/CSV export          | ✅           | ❌           |

RagEval.NET does not aim to replicate every RAGAS feature. It focuses on the four metrics most
critical to production RAG quality and adds .NET-native features — Microsoft.Extensions.AI
compatibility and deep Azure OpenAI integration — that Python RAGAS cannot offer in a .NET
pipeline. CI threshold assertions (see [CI Pipeline Integration](#ci-pipeline-integration)) and
structured export (see [Export results](#export-results)) already ship today, not as a roadmap
item.

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

## Using with Microsoft.Extensions.AI

The optional `RagEval.Extensions.AI` package adapts any [Microsoft.Extensions.AI](https://learn.microsoft.com/dotnet/ai/microsoft-extensions-ai)
`IChatClient` into an `ILlmJudge`, so RagEval.NET can use Anthropic Claude, Azure OpenAI, OpenAI,
Ollama, Mistral, or any future provider that ships an `IChatClient` implementation — all through
the same `RagEvaluatorBuilder`. The base `RagEval.NET` package has no dependency on
Microsoft.Extensions.AI; you only pull it in if you want this flexibility.

```bash
dotnet add package RagEval.Extensions.AI
```

Anthropic Claude as the judge, via the official [Anthropic C# SDK](https://www.nuget.org/packages/Anthropic)
(also requires `dotnet add package Anthropic`, which ships an `AsIChatClient()` adapter):

```csharp
using Anthropic;
using Microsoft.Extensions.AI;
using RagEval;
using RagEval.Extensions.AI;

// Reads the ANTHROPIC_API_KEY environment variable.
IChatClient claude = new AnthropicClient().AsIChatClient("claude-opus-4-8");

var evaluator = new RagEvaluatorBuilder()
    .UseExtensionsAI(claude)
    .WithJudgeModel("claude-opus-4-8")
    .WithMaxConcurrency(5)
    .Build();
```

`claude-opus-4-8` is Anthropic's most capable Opus-tier model and makes an excellent judge; for
high-volume evaluation runs, `claude-sonnet-5` offers near-Opus quality at lower cost, and
`claude-haiku-4-5` is the fastest and cheapest option.

Local-first evaluation with [Ollama](https://learn.microsoft.com/dotnet/ai/quickstarts/quickstart-ollama)
(also requires `dotnet add package OllamaSharp`, which implements `IChatClient` directly):

```csharp
using Microsoft.Extensions.AI;
using OllamaSharp;
using RagEval;
using RagEval.Extensions.AI;

IChatClient ollama = new OllamaApiClient(new Uri("http://localhost:11434"), "llama3.1");

var evaluator = new RagEvaluatorBuilder()
    .UseExtensionsAI(ollama)
    .WithMaxConcurrency(5)
    .Build();
```

Azure OpenAI via `IChatClient` (useful if you already have an `IChatClient` pipeline configured,
e.g. with caching, logging, or telemetry middleware — also requires
`dotnet add package Microsoft.Extensions.AI.OpenAI`, which provides the `AsIChatClient()` adapter):

```csharp
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using RagEval;
using RagEval.Extensions.AI;

IChatClient chatClient = new AzureOpenAIClient(new Uri(endpoint), new AzureKeyCredential(apiKey))
    .GetChatClient(deploymentName)
    .AsIChatClient();

var evaluator = new RagEvaluatorBuilder()
    .UseExtensionsAI(chatClient)
    .WithMaxConcurrency(5)
    .Build();
```

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

## CI Pipeline Integration

Configure minimum acceptable scores with `WithThresholds`, then use `EvaluateAndAssertAsync` (or
`EvaluateBatchAndAssertAsync`) in a `dotnet test` run to fail the build when RAG output quality
regresses below your bar. Metrics with no configured threshold are still scored but never cause
a failure, and a `null` score (e.g. a judge parse failure, or `ContextRecall` with no
`GroundTruth`) always fails a metric that does have a threshold set.

```csharp
using RagEval;
using RagEval.Exceptions;

var evaluator = new RagEvaluatorBuilder()
    .UseAzureOpenAI(endpoint, apiKey, deploymentName)
    .WithThresholds(faithfulness: 0.8, answerRelevance: 0.7, contextPrecision: 0.6)
    .Build();

// e.g. inside an xUnit [Fact] that runs as part of CI:
await evaluator.EvaluateAndAssertAsync(input);
```

`EvaluateAndAssertAsync` throws `EvaluationThresholdException` when one or more metrics fall
short; its `Failures` dictionary lists the required vs. actual score for each failed metric, and
its `Message` spells them out (e.g. `"Faithfulness below threshold: required 0.80, actual 0.62"`).
`EvaluateBatchAndAssertAsync` throws an `AggregateException` containing one
`EvaluationThresholdException` per failing result in the batch:

```csharp
try
{
    await evaluator.EvaluateBatchAndAssertAsync(inputs);
}
catch (AggregateException ex)
{
    foreach (EvaluationThresholdException failure in ex.InnerExceptions.Cast<EvaluationThresholdException>())
    {
        Console.WriteLine(failure.Message);
    }

    throw;
}
```

Calling either method without first configuring thresholds via `WithThresholds` throws
`InvalidOperationException`.

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
