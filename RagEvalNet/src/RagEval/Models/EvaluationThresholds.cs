namespace RagEval.Models;

/// <summary>
/// Minimum acceptable scores for each metric. Used by <see cref="RagEval.RagEvaluator.EvaluateAndAssertAsync"/>
/// and <see cref="RagEval.RagEvaluator.EvaluateBatchAndAssertAsync"/> to fail a CI pipeline when RAG output
/// quality regresses below an agreed bar. A metric with a null threshold is not checked.
/// </summary>
public sealed class EvaluationThresholds
{
    /// <summary>Minimum acceptable <see cref="RagEvaluationResult.Faithfulness"/> score, or null to skip this check.</summary>
    public double? Faithfulness { get; init; }

    /// <summary>Minimum acceptable <see cref="RagEvaluationResult.AnswerRelevance"/> score, or null to skip this check.</summary>
    public double? AnswerRelevance { get; init; }

    /// <summary>Minimum acceptable <see cref="RagEvaluationResult.ContextPrecision"/> score, or null to skip this check.</summary>
    public double? ContextPrecision { get; init; }

    /// <summary>Minimum acceptable <see cref="RagEvaluationResult.ContextRecall"/> score, or null to skip this check.</summary>
    public double? ContextRecall { get; init; }
}
