namespace RagEval.Metrics;

/// <summary>Well-known metric names shared between the metric evaluators and <see cref="RagEval.RagEvaluator"/>.</summary>
internal static class MetricNames
{
    public const string Faithfulness = "Faithfulness";
    public const string AnswerRelevance = "AnswerRelevance";
    public const string ContextPrecision = "ContextPrecision";
    public const string ContextRecall = "ContextRecall";
}
