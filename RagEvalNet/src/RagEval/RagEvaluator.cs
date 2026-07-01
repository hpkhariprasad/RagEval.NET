using RagEval.Judges;
using RagEval.Metrics;
using RagEval.Models;

namespace RagEval;

/// <summary>
/// Evaluates RAG pipeline output quality by scoring faithfulness, answer relevance, context
/// precision, and (when a ground truth is supplied) context recall using an LLM judge.
/// Instances are created via <see cref="RagEvaluatorBuilder"/>.
/// </summary>
public sealed class RagEvaluator
{
    private readonly ILlmJudge _judge;
    private readonly IReadOnlyList<IMetricEvaluator> _metricEvaluators;
    private readonly SemaphoreSlim _concurrencyLimiter;

    /// <summary>The judge model name configured via <see cref="RagEvaluatorBuilder.WithJudgeModel"/>, if any.</summary>
    public string? JudgeModel { get; }

    internal RagEvaluator(ILlmJudge judge, IReadOnlyList<IMetricEvaluator> metricEvaluators, int maxConcurrency, string? judgeModel)
    {
        _judge = judge;
        _metricEvaluators = metricEvaluators;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        JudgeModel = judgeModel;
    }

    /// <summary>
    /// Scores a single question/answer/context input across all applicable metrics.
    /// </summary>
    public async Task<RagEvaluationResult> EvaluateAsync(RagEvaluationInput input, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var result = new RagEvaluationResult { Input = input };

        var metricTasks = _metricEvaluators
            .Select(evaluator => EvaluateMetricAsync(evaluator, input, ct))
            .ToList();

        (string MetricName, MetricEvaluationOutcome Outcome)[] outcomes = await Task.WhenAll(metricTasks).ConfigureAwait(false);

        foreach ((string metricName, MetricEvaluationOutcome outcome) in outcomes)
        {
            result.Reasoning[metricName] = outcome.Reasoning;

            switch (metricName)
            {
                case MetricNames.Faithfulness:
                    result.Faithfulness = outcome.Score;
                    break;
                case MetricNames.AnswerRelevance:
                    result.AnswerRelevance = outcome.Score;
                    break;
                case MetricNames.ContextPrecision:
                    result.ContextPrecision = outcome.Score;
                    break;
                case MetricNames.ContextRecall:
                    result.ContextRecall = outcome.Score;
                    break;
            }
        }

        return result;
    }

    /// <summary>
    /// Scores a batch of inputs, bounding the number of concurrent judge calls to the configured
    /// max concurrency.
    /// </summary>
    public async Task<IReadOnlyList<RagEvaluationResult>> EvaluateBatchAsync(IReadOnlyList<RagEvaluationInput> inputs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        RagEvaluationResult[] results = await Task.WhenAll(inputs.Select(input => EvaluateAsync(input, ct))).ConfigureAwait(false);
        return results;
    }

    private async Task<(string MetricName, MetricEvaluationOutcome Outcome)> EvaluateMetricAsync(
        IMetricEvaluator evaluator, RagEvaluationInput input, CancellationToken ct)
    {
        await _concurrencyLimiter.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(input, _judge, ct).ConfigureAwait(false);
            return (evaluator.MetricName, outcome);
        }
        finally
        {
            _concurrencyLimiter.Release();
        }
    }
}
