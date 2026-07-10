using RagEval.Assertions;
using RagEval.Exceptions;
using RagEval.Models;
using Xunit;

namespace RagEval.Tests.Thresholds;

public class FluentAssertionTests
{
    private static RagEvaluationResult CreateResult(
        double? faithfulness = 0.9,
        double? answerRelevance = 0.9,
        string question = "What is the notice period for termination?") => new()
    {
        Input = new RagEvaluationInput
        {
            Question = question,
            Answer = "The notice period is 30 days.",
            Contexts = ["Clause 12.1: 30 days written notice is required."]
        },
        Faithfulness = faithfulness,
        AnswerRelevance = answerRelevance,
        ContextPrecision = 1.0,
        ContextRecall = null
    };

    [Fact]
    public void Assert_AllResultsPass_ReturnsSameResults()
    {
        IReadOnlyList<RagEvaluationResult> results = [CreateResult(), CreateResult()];

        IReadOnlyList<RagEvaluationResult> returned = results.Assert(
            m => m.Faithfulness >= 0.8,
            m => m.AnswerRelevance >= 0.7);

        Xunit.Assert.Same(results, returned);
    }

    [Fact]
    public void Assert_ScoreBelowThreshold_ThrowsWithActualScoreAndQuestion()
    {
        IReadOnlyList<RagEvaluationResult> results =
            [CreateResult(), CreateResult(faithfulness: 0.62, question: "Which clause covers termination?")];

        var exception = Xunit.Assert.Throws<RagEvalAssertionException>(
            () => results.Assert(m => m.Faithfulness >= 0.8));

        Xunit.Assert.Contains("FAILED", exception.Message);
        Xunit.Assert.Contains("m.Faithfulness", exception.Message);
        Xunit.Assert.Contains("Faithfulness = 0.62", exception.Message);
        Xunit.Assert.Contains("result[1]", exception.Message);
        Xunit.Assert.Contains("Which clause covers termination?", exception.Message);
        Xunit.Assert.Contains("1 of 2 result(s) failed", exception.Message);
    }

    [Fact]
    public void Assert_NullScore_FailsAndReportsNoScore()
    {
        IReadOnlyList<RagEvaluationResult> results = [CreateResult(faithfulness: null)];

        var exception = Xunit.Assert.Throws<RagEvalAssertionException>(
            () => results.Assert(m => m.Faithfulness >= 0.8));

        Xunit.Assert.Contains("Faithfulness = N/A (no score)", exception.Message);
    }

    [Fact]
    public void Assert_MixedAssertions_ReportsPassedAndFailedSeparately()
    {
        IReadOnlyList<RagEvaluationResult> results = [CreateResult(faithfulness: 0.5)];

        var exception = Xunit.Assert.Throws<RagEvalAssertionException>(
            () => results.Assert(
                m => m.Faithfulness >= 0.8,
                m => m.AnswerRelevance >= 0.7));

        Xunit.Assert.Single(exception.Failures);
        Xunit.Assert.Single(exception.PassedAssertions);
        Xunit.Assert.Contains("PASSED", exception.Message);
        Xunit.Assert.Contains("m.AnswerRelevance", exception.Message);
    }

    [Fact]
    public void Assert_MultiMetricAssertion_ReportsEveryReferencedMetric()
    {
        IReadOnlyList<RagEvaluationResult> results = [CreateResult(faithfulness: 0.5, answerRelevance: 0.6)];

        var exception = Xunit.Assert.Throws<RagEvalAssertionException>(
            () => results.Assert(m => m.Faithfulness >= 0.8 && m.AnswerRelevance >= 0.7));

        Xunit.Assert.Contains("Faithfulness = 0.50", exception.Message);
        Xunit.Assert.Contains("AnswerRelevance = 0.60", exception.Message);
    }

    [Fact]
    public void Assert_ManyFailures_TruncatesListedResults()
    {
        List<RagEvaluationResult> results = [];
        for (int i = 0; i < 15; i++)
        {
            results.Add(CreateResult(faithfulness: 0.1));
        }

        var exception = Xunit.Assert.Throws<RagEvalAssertionException>(
            () => ((IReadOnlyList<RagEvaluationResult>)results).Assert(m => m.Faithfulness >= 0.8));

        Xunit.Assert.Contains("... and 5 more", exception.Message);
        Xunit.Assert.Equal(15, exception.Failures[0].FailingResults.Count);
    }

    [Fact]
    public void Assert_NoAssertions_ThrowsArgumentException()
    {
        IReadOnlyList<RagEvaluationResult> results = [CreateResult()];

        Xunit.Assert.Throws<ArgumentException>(() => results.Assert());
    }

    [Fact]
    public void Assert_OnSummary_PassesAndFailsOnAverages()
    {
        IReadOnlyList<RagEvaluationResult> results = [CreateResult(faithfulness: 0.6), CreateResult(faithfulness: 0.8)];
        RagEvaluationSummary summary = results.GetSummary();

        summary.Assert(s => s.AvgFaithfulness >= 0.7);

        var exception = Xunit.Assert.Throws<RagEvalAssertionException>(
            () => summary.Assert(s => s.AvgFaithfulness >= 0.9));

        Xunit.Assert.Contains("AvgFaithfulness = 0.70", exception.Message);
        Xunit.Assert.Equal(2, exception.TotalResults);
    }

    [Fact]
    public void Assert_OnSummary_NullAverageFailsComparison()
    {
        IReadOnlyList<RagEvaluationResult> results = [CreateResult()];
        RagEvaluationSummary summary = results.GetSummary();

        var exception = Xunit.Assert.Throws<RagEvalAssertionException>(
            () => summary.Assert(s => s.AvgContextRecall >= 0.5));

        Xunit.Assert.Contains("AvgContextRecall = N/A (no score)", exception.Message);
    }
}
