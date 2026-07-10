# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-07-10

### Added

- `RagEval.Extensions.AI` package: `ExtensionsAIJudge` and the `UseExtensionsAI` builder
  extension adapt any Microsoft.Extensions.AI `IChatClient` into an `ILlmJudge`, so any
  M.E.AI-compatible provider (Anthropic Claude, Azure OpenAI, OpenAI, Ollama, Mistral, ...)
  works as the LLM judge with zero custom code.
- README section on Microsoft.Extensions.AI usage, including an Anthropic Claude example
  built on the official Anthropic C# SDK's `AsIChatClient()` adapter.
- Versioned, documented export schemas: the JSON export now emits a stable camelCase
  `RagEvalJsonExport` document (`schemaVersion`, `generatedAt`, `summary`, `results`), and the
  CSV column set is versioned with an append-only guarantee. Current versions are exposed via
  `RagEvalExportSchema`.
- Fluent threshold assertions for CI pipelines: `results.Assert(m => m.Faithfulness >= 0.8)`
  and `summary.Assert(s => s.AvgFaithfulness >= 0.85)` (in `RagEval.Assertions`) throw a
  `RagEvalAssertionException` whose message diffs each failed assertion against the actual
  scores of every failing result. README gains a GitHub Actions "fail the build if RAG quality
  regresses" example.

### Changed

- **Breaking (JSON export):** `JsonRagEvalExporter` no longer writes a bare PascalCase array of
  results; it writes the versioned camelCase envelope described in the README (schema v1.0).

## [1.0.0] - 2026-07-01

### Added

- Initial release of RagEval.NET.
- `RagEvaluator` and fluent `RagEvaluatorBuilder` for scoring RAG pipeline output.
- Four LLM-as-judge metrics: `FaithfulnessEvaluator`, `AnswerRelevanceEvaluator`,
  `ContextPrecisionEvaluator`, and `ContextRecallEvaluator`.
- `ILlmJudge` abstraction with `AzureOpenAIJudge` and `OpenAIJudge` implementations.
- Single (`EvaluateAsync`) and batch (`EvaluateBatchAsync`) evaluation, with
  `IReadOnlyList<RagEvaluationResult>.GetSummary()` for aggregate scoring.
- xUnit test suite covering happy path, partial score, zero score, unparseable judge
  responses, and cancellation for every metric.
- Sample console app demonstrating single and batch evaluation against Azure OpenAI.
