using Moq;
using RagEval.Exceptions;
using RagEval.Judges;
using RagEval.Models;
using Xunit;

namespace RagEval.Tests.Thresholds;

public class ThresholdAssertionTests
{
    private static RagEvaluationInput CreateInput(string contextMarker = "PASS", string? groundTruth = null) => new()
    {
        Question = "What is the notice period for termination?",
        Answer = "The notice period is 30 days.",
        Contexts = [$"{contextMarker} marker context: 30 days written notice is required."],
        GroundTruth = groundTruth
    };

    private static Mock<ILlmJudge> CreateJudge(
        bool faithfulnessSupportedForPassMarker = true,
        bool answerRelevanceSimilar = true,
        bool? contextRecallCovered = null)
    {
        var judge = new Mock<ILlmJudge>();

        judge.Setup(j => j.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("Extract all distinct factual claims made in the following answer")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"claims":["single claim"]}""");

        judge.Setup(j => j.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("Determine whether the following claim is fully supported") && p.Contains("PASS marker")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(faithfulnessSupportedForPassMarker
                ? """{"supported":true,"reason":"supported by PASS context"}"""
                : """{"supported":false,"reason":"not supported"}""");

        judge.Setup(j => j.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("Determine whether the following claim is fully supported") && p.Contains("FAIL marker")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"supported":false,"reason":"not supported by FAIL context"}""");

        judge.Setup(j => j.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("generate exactly 3 questions")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"questions":["q1","q2","q3"]}""");

        judge.Setup(j => j.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("Score how semantically similar")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(answerRelevanceSimilar
                ? """{"similarity":1.0}"""
                : """{"similarity":0.0}""");

        judge.Setup(j => j.CompleteAsync(
                It.IsAny<string>(),
                It.Is<string>(p => p.Contains("Determine whether the following context chunk contributed")),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"useful":true,"reason":"contributed to the answer"}""");

        if (contextRecallCovered is not null)
        {
            judge.Setup(j => j.CompleteAsync(
                    It.IsAny<string>(),
                    It.Is<string>(p => p.Contains("Extract all distinct factual claims made in the following ground truth answer")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync("""{"claims":["single ground truth claim"]}""");

            judge.Setup(j => j.CompleteAsync(
                    It.IsAny<string>(),
                    It.Is<string>(p => p.Contains("Determine whether the following claim from a ground truth answer is present")),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(contextRecallCovered.Value
                    ? """{"covered":true,"reason":"covered"}"""
                    : """{"covered":false,"reason":"not covered"}""");
        }

        return judge;
    }

    [Fact]
    public async Task EvaluateAndAssertAsync_AllMetricsPassThresholds_ReturnsResultWithoutThrowing()
    {
        Mock<ILlmJudge> judge = CreateJudge(faithfulnessSupportedForPassMarker: true, contextRecallCovered: true);

        RagEvaluator evaluator = new RagEvaluatorBuilder()
            .UseJudge(judge.Object)
            .WithThresholds(faithfulness: 0.5, answerRelevance: 0.5, contextPrecision: 0.5, contextRecall: 0.5)
            .Build();

        RagEvaluationResult result = await evaluator.EvaluateAndAssertAsync(CreateInput(groundTruth: "30 days written notice"));

        Assert.Equal(1.0, result.Faithfulness);
        Assert.Equal(1.0, result.AnswerRelevance);
        Assert.Equal(1.0, result.ContextPrecision);
        Assert.Equal(1.0, result.ContextRecall);
    }

    [Fact]
    public async Task EvaluateAndAssertAsync_SingleMetricFails_ThrowsWithCorrectFailuresEntry()
    {
        Mock<ILlmJudge> judge = CreateJudge(faithfulnessSupportedForPassMarker: false);

        RagEvaluator evaluator = new RagEvaluatorBuilder()
            .UseJudge(judge.Object)
            .WithThresholds(faithfulness: 0.8)
            .Build();

        EvaluationThresholdException exception = await Assert.ThrowsAsync<EvaluationThresholdException>(
            () => evaluator.EvaluateAndAssertAsync(CreateInput()));

        (double Required, double Actual) failure = Assert.Single(exception.Failures).Value;
        Assert.True(exception.Failures.ContainsKey("Faithfulness"));
        Assert.Equal(0.8, failure.Required);
        Assert.Equal(0.0, failure.Actual);
        Assert.Contains("Faithfulness below threshold: required 0.80, actual 0.00", exception.Message);
    }

    [Fact]
    public async Task EvaluateAndAssertAsync_MultipleMetricsFail_AllListedInFailures()
    {
        Mock<ILlmJudge> judge = CreateJudge(faithfulnessSupportedForPassMarker: false, answerRelevanceSimilar: false);

        RagEvaluator evaluator = new RagEvaluatorBuilder()
            .UseJudge(judge.Object)
            .WithThresholds(faithfulness: 0.8, answerRelevance: 0.8)
            .Build();

        EvaluationThresholdException exception = await Assert.ThrowsAsync<EvaluationThresholdException>(
            () => evaluator.EvaluateAndAssertAsync(CreateInput()));

        Assert.Equal(2, exception.Failures.Count);
        Assert.True(exception.Failures.ContainsKey("Faithfulness"));
        Assert.True(exception.Failures.ContainsKey("AnswerRelevance"));
    }

    [Fact]
    public async Task EvaluateAndAssertAsync_NullScoreWithThresholdSet_Fails()
    {
        // GroundTruth is omitted, so ContextRecall is never scored (null), but a threshold is set for it.
        Mock<ILlmJudge> judge = CreateJudge();

        RagEvaluator evaluator = new RagEvaluatorBuilder()
            .UseJudge(judge.Object)
            .WithThresholds(contextRecall: 0.5)
            .Build();

        EvaluationThresholdException exception = await Assert.ThrowsAsync<EvaluationThresholdException>(
            () => evaluator.EvaluateAndAssertAsync(CreateInput(groundTruth: null)));

        (double Required, double Actual) failure = Assert.Single(exception.Failures).Value;
        Assert.True(exception.Failures.ContainsKey("ContextRecall"));
        Assert.Equal(0.5, failure.Required);
        Assert.True(double.IsNaN(failure.Actual));
        Assert.Contains("ContextRecall below threshold: required 0.50, actual N/A (no score)", exception.Message);
    }

    [Fact]
    public async Task EvaluateAndAssertAsync_NoThresholdsConfigured_ThrowsInvalidOperationException()
    {
        Mock<ILlmJudge> judge = CreateJudge();

        RagEvaluator evaluator = new RagEvaluatorBuilder()
            .UseJudge(judge.Object)
            .Build();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => evaluator.EvaluateAndAssertAsync(CreateInput()));

        Assert.Equal(
            "No thresholds configured. Call WithThresholds() on the builder before using EvaluateAndAssertAsync.",
            exception.Message);
    }

    [Fact]
    public async Task EvaluateBatchAndAssertAsync_TwoOfThreeFail_AggregateExceptionHasTwoInnerExceptions()
    {
        Mock<ILlmJudge> judge = CreateJudge();

        RagEvaluator evaluator = new RagEvaluatorBuilder()
            .UseJudge(judge.Object)
            .WithThresholds(faithfulness: 0.5)
            .Build();

        List<RagEvaluationInput> inputs =
        [
            CreateInput(contextMarker: "PASS"),
            CreateInput(contextMarker: "FAIL"),
            CreateInput(contextMarker: "FAIL")
        ];

        AggregateException exception = await Assert.ThrowsAsync<AggregateException>(
            () => evaluator.EvaluateBatchAndAssertAsync(inputs));

        Assert.Equal(2, exception.InnerExceptions.Count);
        Assert.All(exception.InnerExceptions, inner => Assert.IsType<EvaluationThresholdException>(inner));
    }

    [Fact]
    public async Task EvaluateBatchAndAssertAsync_NoThresholdsConfigured_ThrowsInvalidOperationException()
    {
        Mock<ILlmJudge> judge = CreateJudge();

        RagEvaluator evaluator = new RagEvaluatorBuilder()
            .UseJudge(judge.Object)
            .Build();

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => evaluator.EvaluateBatchAndAssertAsync([CreateInput()]));
    }

    [Fact]
    public async Task EvaluateAndAssertAsync_CancelledToken_PropagatesOperationCanceledException()
    {
        Mock<ILlmJudge> judge = new();

        RagEvaluator evaluator = new RagEvaluatorBuilder()
            .UseJudge(judge.Object)
            .WithThresholds(faithfulness: 0.5)
            .Build();

        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => evaluator.EvaluateAndAssertAsync(CreateInput(), cts.Token));

        judge.Verify(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateBatchAndAssertAsync_CancelledToken_PropagatesOperationCanceledException()
    {
        Mock<ILlmJudge> judge = new();

        RagEvaluator evaluator = new RagEvaluatorBuilder()
            .UseJudge(judge.Object)
            .WithThresholds(faithfulness: 0.5)
            .Build();

        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => evaluator.EvaluateBatchAndAssertAsync([CreateInput()], cts.Token));
    }
}
