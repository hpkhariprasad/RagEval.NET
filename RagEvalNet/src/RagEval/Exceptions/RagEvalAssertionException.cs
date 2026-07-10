using System.Globalization;
using System.Text;
using RagEval.Models;

namespace RagEval.Exceptions;

/// <summary>
/// Thrown by <c>RagEval.Assertions.RagEvalAssertionExtensions.Assert</c> when one or more fluent
/// assertions fail. The message is a rich diff designed to be read straight from CI logs: each
/// failed assertion is listed with the actual score of every metric it references, per failing
/// result, alongside the assertions that passed.
/// </summary>
public class RagEvalAssertionException : Exception
{
    private const int MaxListedFailuresPerAssertion = 10;
    private const int MaxQuestionLength = 70;

    /// <summary>The assertions that failed, with the results that failed each of them.</summary>
    public IReadOnlyList<RagEvalAssertionFailure> Failures { get; }

    /// <summary>The string form of the assertions that passed.</summary>
    public IReadOnlyList<string> PassedAssertions { get; }

    /// <summary>The total number of results (or summarized results) the assertions ran against.</summary>
    public int TotalResults { get; }

    /// <summary>Creates an exception describing the given assertion failures.</summary>
    public RagEvalAssertionException(
        IReadOnlyList<RagEvalAssertionFailure> failures,
        IReadOnlyList<string> passedAssertions,
        int totalResults)
        : base(BuildMessage(failures, passedAssertions, totalResults))
    {
        Failures = failures;
        PassedAssertions = passedAssertions;
        TotalResults = totalResults;
    }

    private static string BuildMessage(
        IReadOnlyList<RagEvalAssertionFailure> failures,
        IReadOnlyList<string> passedAssertions,
        int totalResults)
    {
        StringBuilder builder = new();
        int totalAssertions = failures.Count + passedAssertions.Count;
        builder.AppendLine(
            $"RAG quality assertion failed: {failures.Count} of {totalAssertions} assertion(s) " +
            $"failed across {totalResults} result(s).");

        foreach (RagEvalAssertionFailure failure in failures)
        {
            builder.AppendLine();
            builder.AppendLine($"FAILED  {failure.Assertion}");
            AppendFailureDetail(builder, failure);
        }

        foreach (string passed in passedAssertions)
        {
            builder.AppendLine();
            builder.Append("PASSED  ").AppendLine(passed);
        }

        return builder.ToString().TrimEnd();
    }

    private static void AppendFailureDetail(StringBuilder builder, RagEvalAssertionFailure failure)
    {
        if (failure.Summary is not null)
        {
            builder.AppendLine($"        actual: {FormatSummaryMetrics(failure)}");
            return;
        }

        builder.AppendLine($"        {failure.FailingResults.Count} of {failure.TotalResults} result(s) failed:");

        foreach (RagEvalAssertionFailure.FailingResult failing in failure.FailingResults.Take(MaxListedFailuresPerAssertion))
        {
            string metrics = FormatResultMetrics(failure.MetricNames, failing.Result);
            string question = Truncate(failing.Result.Input.Question);
            builder.AppendLine($"        - result[{failing.Index}]: {metrics} | Q: \"{question}\"");
        }

        if (failure.FailingResults.Count > MaxListedFailuresPerAssertion)
        {
            builder.AppendLine($"        ... and {failure.FailingResults.Count - MaxListedFailuresPerAssertion} more");
        }
    }

    private static string FormatResultMetrics(IReadOnlyList<string> metricNames, RagEvaluationResult result)
    {
        if (metricNames.Count == 0)
        {
            return "assertion evaluated to false";
        }

        IEnumerable<string> parts = metricNames.Select(name => $"{name} = {FormatScore(GetResultMetric(result, name))}");
        return string.Join(", ", parts);
    }

    private static string FormatSummaryMetrics(RagEvalAssertionFailure failure)
    {
        if (failure.MetricNames.Count == 0)
        {
            return "assertion evaluated to false";
        }

        IEnumerable<string> parts = failure.MetricNames.Select(name =>
            $"{name} = {FormatScore(GetSummaryMetric(failure.Summary!, name))}");
        return string.Join(", ", parts);
    }

    private static double? GetResultMetric(RagEvaluationResult result, string metricName) => metricName switch
    {
        nameof(RagEvaluationResult.Faithfulness) => result.Faithfulness,
        nameof(RagEvaluationResult.AnswerRelevance) => result.AnswerRelevance,
        nameof(RagEvaluationResult.ContextPrecision) => result.ContextPrecision,
        nameof(RagEvaluationResult.ContextRecall) => result.ContextRecall,
        _ => null
    };

    private static double? GetSummaryMetric(RagEvaluationSummary summary, string metricName) => metricName switch
    {
        nameof(RagEvaluationSummary.AvgFaithfulness) => summary.AvgFaithfulness,
        nameof(RagEvaluationSummary.AvgAnswerRelevance) => summary.AvgAnswerRelevance,
        nameof(RagEvaluationSummary.AvgContextPrecision) => summary.AvgContextPrecision,
        nameof(RagEvaluationSummary.AvgContextRecall) => summary.AvgContextRecall,
        _ => null
    };

    private static string FormatScore(double? score) =>
        score is null ? "N/A (no score)" : score.Value.ToString("F2", CultureInfo.InvariantCulture);

    private static string Truncate(string value) =>
        value.Length <= MaxQuestionLength ? value : value[..MaxQuestionLength] + "...";
}

/// <summary>One failed assertion: its string form, the metrics it references, and what failed it.</summary>
public sealed class RagEvalAssertionFailure
{
    /// <summary>The string form of the failed assertion, e.g. <c>m =&gt; (m.Faithfulness &gt;= 0.8)</c>.</summary>
    public string Assertion { get; }

    /// <summary>The metric property names the assertion references, used to report actual values.</summary>
    public IReadOnlyList<string> MetricNames { get; }

    /// <summary>The results that failed the assertion, with their batch indices. Empty for summary assertions.</summary>
    public IReadOnlyList<FailingResult> FailingResults { get; }

    /// <summary>The total number of results the assertion ran against.</summary>
    public int TotalResults { get; }

    /// <summary>The summary the assertion ran against, when it was a summary-level assertion.</summary>
    public RagEvaluationSummary? Summary { get; }

    /// <summary>Creates a failure record for a per-result assertion.</summary>
    public RagEvalAssertionFailure(
        string assertion,
        IReadOnlyList<string> metricNames,
        IReadOnlyList<FailingResult> failingResults,
        int totalResults)
    {
        Assertion = assertion;
        MetricNames = metricNames;
        FailingResults = failingResults;
        TotalResults = totalResults;
    }

    private RagEvalAssertionFailure(string assertion, IReadOnlyList<string> metricNames, RagEvaluationSummary summary)
    {
        Assertion = assertion;
        MetricNames = metricNames;
        FailingResults = [];
        TotalResults = summary.TotalEvaluated;
        Summary = summary;
    }

    /// <summary>Creates a failure record for a summary-level assertion.</summary>
    public static RagEvalAssertionFailure ForSummary(
        string assertion, IReadOnlyList<string> metricNames, RagEvaluationSummary summary) =>
        new(assertion, metricNames, summary);

    /// <summary>A single result that failed an assertion, with its index in the evaluated batch.</summary>
    public sealed record FailingResult(int Index, RagEvaluationResult Result);
}
