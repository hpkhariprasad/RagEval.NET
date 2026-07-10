using System.Linq.Expressions;
using RagEval.Exceptions;
using RagEval.Models;

namespace RagEval.Assertions;

/// <summary>
/// Fluent threshold assertions over evaluation results, designed for CI pipelines: each
/// assertion is an expression such as <c>m =&gt; m.Faithfulness &gt;= 0.8</c>, and any failure
/// throws a <see cref="RagEvalAssertionException"/> whose message diffs the expected condition
/// against the actual scores of every failing result.
/// </summary>
public static class RagEvalAssertionExtensions
{
    /// <summary>
    /// Asserts that every result satisfies every assertion, e.g.
    /// <c>results.Assert(m =&gt; m.Faithfulness &gt;= 0.8)</c>. A null metric score fails any
    /// comparison it appears in, so unscored metrics fail the assertion rather than pass silently.
    /// </summary>
    /// <param name="results">The results to check.</param>
    /// <param name="assertions">One or more per-result conditions that must all hold.</param>
    /// <returns>The unchanged <paramref name="results"/>, for further chaining.</returns>
    /// <exception cref="RagEvalAssertionException">One or more results failed an assertion.</exception>
    public static IReadOnlyList<RagEvaluationResult> Assert(
        this IReadOnlyList<RagEvaluationResult> results,
        params Expression<Func<RagEvaluationResult, bool>>[] assertions)
    {
        ArgumentNullException.ThrowIfNull(results);
        ValidateAssertions(assertions);

        List<RagEvalAssertionFailure> failures = [];
        List<string> passedAssertions = [];

        foreach (Expression<Func<RagEvaluationResult, bool>> assertion in assertions)
        {
            Func<RagEvaluationResult, bool> predicate = assertion.Compile();
            IReadOnlyList<string> metricNames = MetricMemberVisitor.CollectMetricNames(assertion);

            List<RagEvalAssertionFailure.FailingResult> failing = [];
            for (int index = 0; index < results.Count; index++)
            {
                if (!predicate(results[index]))
                {
                    failing.Add(new RagEvalAssertionFailure.FailingResult(index, results[index]));
                }
            }

            if (failing.Count > 0)
            {
                failures.Add(new RagEvalAssertionFailure(assertion.ToString(), metricNames, failing, results.Count));
            }
            else
            {
                passedAssertions.Add(assertion.ToString());
            }
        }

        if (failures.Count > 0)
        {
            throw new RagEvalAssertionException(failures, passedAssertions, results.Count);
        }

        return results;
    }

    /// <summary>
    /// Asserts that the batch summary satisfies every assertion, e.g.
    /// <c>results.GetSummary().Assert(s =&gt; s.AvgFaithfulness &gt;= 0.85)</c>. A null average
    /// (no result produced a score for that metric) fails any comparison it appears in.
    /// </summary>
    /// <param name="summary">The summary to check.</param>
    /// <param name="assertions">One or more conditions on the summary that must all hold.</param>
    /// <returns>The unchanged <paramref name="summary"/>, for further chaining.</returns>
    /// <exception cref="RagEvalAssertionException">The summary failed an assertion.</exception>
    public static RagEvaluationSummary Assert(
        this RagEvaluationSummary summary,
        params Expression<Func<RagEvaluationSummary, bool>>[] assertions)
    {
        ArgumentNullException.ThrowIfNull(summary);
        ValidateAssertions(assertions);

        List<RagEvalAssertionFailure> failures = [];
        List<string> passedAssertions = [];

        foreach (Expression<Func<RagEvaluationSummary, bool>> assertion in assertions)
        {
            if (!assertion.Compile()(summary))
            {
                IReadOnlyList<string> metricNames = MetricMemberVisitor.CollectMetricNames(assertion);
                failures.Add(RagEvalAssertionFailure.ForSummary(assertion.ToString(), metricNames, summary));
            }
            else
            {
                passedAssertions.Add(assertion.ToString());
            }
        }

        if (failures.Count > 0)
        {
            throw new RagEvalAssertionException(failures, passedAssertions, summary.TotalEvaluated);
        }

        return summary;
    }

    private static void ValidateAssertions<T>(Expression<Func<T, bool>>[] assertions)
    {
        ArgumentNullException.ThrowIfNull(assertions);
        if (assertions.Length == 0)
        {
            throw new ArgumentException("At least one assertion is required.", nameof(assertions));
        }
    }

    /// <summary>
    /// Collects the names of nullable-double properties read off the lambda parameter, so the
    /// failure diff can report the actual value of each metric the assertion refers to.
    /// </summary>
    private sealed class MetricMemberVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter;
        private readonly List<string> _metricNames = [];

        private MetricMemberVisitor(ParameterExpression parameter)
        {
            _parameter = parameter;
        }

        public static IReadOnlyList<string> CollectMetricNames(LambdaExpression assertion)
        {
            var visitor = new MetricMemberVisitor(assertion.Parameters[0]);
            visitor.Visit(assertion.Body);
            return visitor._metricNames;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            if (node.Expression == _parameter && node.Type == typeof(double?) && !_metricNames.Contains(node.Member.Name))
            {
                _metricNames.Add(node.Member.Name);
            }

            return base.VisitMember(node);
        }
    }
}
