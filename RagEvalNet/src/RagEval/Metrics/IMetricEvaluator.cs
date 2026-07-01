using RagEval.Judges;
using RagEval.Models;

namespace RagEval.Metrics;

/// <summary>
/// The outcome of scoring a single metric for a single <see cref="RagEvaluationInput"/>.
/// </summary>
/// <param name="Score">The computed score in the range 0.0-1.0, or null if it could not be computed.</param>
/// <param name="Reasoning">A human-readable explanation of how the score was derived.</param>
public sealed record MetricEvaluationOutcome(double? Score, string Reasoning);

/// <summary>
/// Computes a single RAG quality metric using an <see cref="ILlmJudge"/>.
/// </summary>
public interface IMetricEvaluator
{
    /// <summary>The name of the metric, used as the key in <see cref="RagEvaluationResult.Reasoning"/>.</summary>
    string MetricName { get; }

    /// <summary>
    /// Scores the given input, using the supplied judge to answer any sub-questions the metric requires.
    /// </summary>
    Task<MetricEvaluationOutcome> EvaluateAsync(RagEvaluationInput input, ILlmJudge judge, CancellationToken ct = default);
}
