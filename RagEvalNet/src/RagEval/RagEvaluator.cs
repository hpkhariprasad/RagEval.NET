using RagEval.Exceptions;
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
    private const string NoThresholdsMessage =
        "No thresholds configured. Call WithThresholds() on the builder before using EvaluateAndAssertAsync.";

    private readonly ILlmJudge _judge;
    private readonly IReadOnlyList<IMetricEvaluator> _metricEvaluators;
    private readonly SemaphoreSlim _concurrencyLimiter;
    private readonly EvaluationThresholds? _thresholds;

    /// <summary>The judge model name configured via <see cref="RagEvaluatorBuilder.WithJudgeModel"/>, if any.</summary>
    public string? JudgeModel { get; }

    internal RagEvaluator(
        ILlmJudge judge,
        IReadOnlyList<IMetricEvaluator> metricEvaluators,
        int maxConcurrency,
        string? judgeModel,
        EvaluationThresholds? thresholds = null)
    {
        _judge = judge;
        _metricEvaluators = metricEvaluators;
        _concurrencyLimiter = new SemaphoreSlim(maxConcurrency, maxConcurrency);
        JudgeModel = judgeModel;
        _thresholds = thresholds;
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

    /// <summary>
    /// Scores a single input and throws <see cref="EvaluationThresholdException"/> if any metric
    /// with a configured threshold falls below it. Metrics without a configured threshold are not checked.
    /// </summary>
    /// <exception cref="InvalidOperationException">No thresholds were configured via <see cref="RagEvaluatorBuilder.WithThresholds(EvaluationThresholds)"/>.</exception>
    /// <exception cref="EvaluationThresholdException">One or more metric scores fell below their configured threshold.</exception>
    public async Task<RagEvaluationResult> EvaluateAndAssertAsync(RagEvaluationInput input, CancellationToken ct = default)
    {
        if (_thresholds is null)
        {
            throw new InvalidOperationException(NoThresholdsMessage);
        }

        RagEvaluationResult result = await EvaluateAsync(input, ct).ConfigureAwait(false);

        Dictionary<string, (double Required, double Actual)> failures = CollectFailures(result, _thresholds);
        if (failures.Count > 0)
        {
            throw new EvaluationThresholdException(result, _thresholds, failures);
        }

        return result;
    }

    /// <summary>
    /// Scores a batch of inputs and throws an <see cref="AggregateException"/> containing one
    /// <see cref="EvaluationThresholdException"/> for each result that fell below a configured threshold.
    /// </summary>
    /// <exception cref="InvalidOperationException">No thresholds were configured via <see cref="RagEvaluatorBuilder.WithThresholds(EvaluationThresholds)"/>.</exception>
    /// <exception cref="AggregateException">One or more results fell below their configured thresholds.</exception>
    public async Task<IReadOnlyList<RagEvaluationResult>> EvaluateBatchAndAssertAsync(IEnumerable<RagEvaluationInput> inputs, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        if (_thresholds is null)
        {
            throw new InvalidOperationException(NoThresholdsMessage);
        }

        IReadOnlyList<RagEvaluationResult> results = await EvaluateBatchAsync(inputs.ToList(), ct).ConfigureAwait(false);

        List<EvaluationThresholdException> failures = [];
        foreach (RagEvaluationResult result in results)
        {
            Dictionary<string, (double Required, double Actual)> resultFailures = CollectFailures(result, _thresholds);
            if (resultFailures.Count > 0)
            {
                failures.Add(new EvaluationThresholdException(result, _thresholds, resultFailures));
            }
        }

        if (failures.Count > 0)
        {
            throw new AggregateException(failures);
        }

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

    private static Dictionary<string, (double Required, double Actual)> CollectFailures(RagEvaluationResult result, EvaluationThresholds thresholds)
    {
        Dictionary<string, (double Required, double Actual)> failures = [];

        CheckThreshold(failures, MetricNames.Faithfulness, thresholds.Faithfulness, result.Faithfulness);
        CheckThreshold(failures, MetricNames.AnswerRelevance, thresholds.AnswerRelevance, result.AnswerRelevance);
        CheckThreshold(failures, MetricNames.ContextPrecision, thresholds.ContextPrecision, result.ContextPrecision);
        CheckThreshold(failures, MetricNames.ContextRecall, thresholds.ContextRecall, result.ContextRecall);

        return failures;
    }

    private static void CheckThreshold(
        Dictionary<string, (double Required, double Actual)> failures, string metricName, double? required, double? actual)
    {
        if (required is null)
        {
            return;
        }

        if (actual is null || actual.Value < required.Value)
        {
            failures[metricName] = (required.Value, actual ?? double.NaN);
        }
    }
}
