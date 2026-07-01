using System.Globalization;
using RagEval.Models;

namespace RagEval.Exceptions;

/// <summary>
/// Thrown by <see cref="RagEval.RagEvaluator.EvaluateAndAssertAsync"/> when one or more metric
/// scores for a result fall below the configured <see cref="EvaluationThresholds"/>.
/// </summary>
public class EvaluationThresholdException : Exception
{
    /// <summary>The evaluation result that failed one or more thresholds.</summary>
    public RagEvaluationResult Result { get; }

    /// <summary>The thresholds the result was checked against.</summary>
    public EvaluationThresholds Thresholds { get; }

    /// <summary>
    /// The metrics that failed, keyed by metric name, with the required and actual score for each.
    /// A null (unparseable) actual score is represented as <see cref="double.NaN"/>.
    /// </summary>
    public Dictionary<string, (double Required, double Actual)> Failures { get; }

    /// <summary>
    /// Creates an exception describing the given threshold failures.
    /// </summary>
    public EvaluationThresholdException(
        RagEvaluationResult result,
        EvaluationThresholds thresholds,
        Dictionary<string, (double Required, double Actual)> failures)
        : base(BuildMessage(failures))
    {
        Result = result;
        Thresholds = thresholds;
        Failures = failures;
    }

    private static string BuildMessage(Dictionary<string, (double Required, double Actual)> failures)
    {
        IEnumerable<string> lines = failures.Select(failure =>
        {
            string required = failure.Value.Required.ToString("F2", CultureInfo.InvariantCulture);
            string actual = double.IsNaN(failure.Value.Actual)
                ? "N/A (no score)"
                : failure.Value.Actual.ToString("F2", CultureInfo.InvariantCulture);

            return $"{failure.Key} below threshold: required {required}, actual {actual}";
        });

        return string.Join(Environment.NewLine, lines);
    }
}
