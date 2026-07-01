namespace RagEval.Models;

/// <summary>
/// The scored output of evaluating a single <see cref="RagEvaluationInput"/>.
/// </summary>
public sealed class RagEvaluationResult
{
    /// <summary>
    /// Fraction of factual claims in the answer that are supported by the retrieved contexts (0.0-1.0).
    /// Null when the score could not be computed.
    /// </summary>
    public double? Faithfulness { get; set; }

    /// <summary>
    /// Average semantic similarity between the question and questions reverse-engineered from the answer (0.0-1.0).
    /// Null when the score could not be computed.
    /// </summary>
    public double? AnswerRelevance { get; set; }

    /// <summary>
    /// Fraction of retrieved context chunks that contributed to the answer (0.0-1.0).
    /// Null when the score could not be computed.
    /// </summary>
    public double? ContextPrecision { get; set; }

    /// <summary>
    /// Fraction of ground-truth claims that are covered by the retrieved contexts (0.0-1.0).
    /// Null when <see cref="Models.RagEvaluationInput.GroundTruth"/> was not provided, or the score could not be computed.
    /// </summary>
    public double? ContextRecall { get; set; }

    /// <summary>
    /// Human-readable explanations for each metric score, keyed by metric name
    /// (e.g. "Faithfulness", "AnswerRelevance", "ContextPrecision", "ContextRecall").
    /// </summary>
    public Dictionary<string, string> Reasoning { get; set; } = new();

    /// <summary>The input that was evaluated to produce this result.</summary>
    public required RagEvaluationInput Input { get; set; }
}
