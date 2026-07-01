namespace RagEval.Models;

/// <summary>
/// Aggregate metric averages computed across a batch of <see cref="RagEvaluationResult"/> instances.
/// </summary>
public sealed class RagEvaluationSummary
{
    /// <summary>Average <see cref="RagEvaluationResult.Faithfulness"/> across all results with a non-null score.</summary>
    public double? AvgFaithfulness { get; set; }

    /// <summary>Average <see cref="RagEvaluationResult.AnswerRelevance"/> across all results with a non-null score.</summary>
    public double? AvgAnswerRelevance { get; set; }

    /// <summary>Average <see cref="RagEvaluationResult.ContextPrecision"/> across all results with a non-null score.</summary>
    public double? AvgContextPrecision { get; set; }

    /// <summary>Average <see cref="RagEvaluationResult.ContextRecall"/> across all results with a non-null score.</summary>
    public double? AvgContextRecall { get; set; }

    /// <summary>The total number of results the summary was computed from.</summary>
    public int TotalEvaluated { get; set; }
}

/// <summary>
/// Extension methods for summarizing batches of evaluation results.
/// </summary>
public static class RagEvaluationSummaryExtensions
{
    /// <summary>
    /// Computes per-metric averages across a batch of evaluation results, ignoring null scores.
    /// </summary>
    public static RagEvaluationSummary GetSummary(this IReadOnlyList<RagEvaluationResult> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        return new RagEvaluationSummary
        {
            AvgFaithfulness = Average(results.Select(r => r.Faithfulness)),
            AvgAnswerRelevance = Average(results.Select(r => r.AnswerRelevance)),
            AvgContextPrecision = Average(results.Select(r => r.ContextPrecision)),
            AvgContextRecall = Average(results.Select(r => r.ContextRecall)),
            TotalEvaluated = results.Count
        };
    }

    private static double? Average(IEnumerable<double?> values)
    {
        List<double> scored = values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return scored.Count == 0 ? null : scored.Average();
    }
}
