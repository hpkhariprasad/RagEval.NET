using Moq;
using RagEval.Judges;
using RagEval.Metrics;
using RagEval.Models;
using Xunit;

namespace RagEval.Tests;

public class ContextRecallTests
{
    private static RagEvaluationInput CreateInput(string? groundTruth = "30 days written notice per clause 12.1, delivered to the registered office.") => new()
    {
        Question = "What is the notice period for termination?",
        Answer = "The notice period is 30 days.",
        Contexts = ["Clause 12.1: 30 days written notice is required."],
        GroundTruth = groundTruth
    };

    [Fact]
    public async Task EvaluateAsync_AllClaimsCovered_ReturnsPerfectScore()
    {
        var judge = new Mock<ILlmJudge>();
        judge.SetupSequence(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"claims":["30 day notice period","notice delivered to registered office"]}""")
            .ReturnsAsync("""{"covered":true,"reason":"present in context"}""")
            .ReturnsAsync("""{"covered":true,"reason":"present in context"}""");

        var evaluator = new ContextRecallEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Equal(1.0, outcome.Score);
    }

    [Fact]
    public async Task EvaluateAsync_SomeClaimsUncovered_ReturnsPartialScore()
    {
        var judge = new Mock<ILlmJudge>();
        judge.SetupSequence(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"claims":["30 day notice period","notice delivered to registered office"]}""")
            .ReturnsAsync("""{"covered":true,"reason":"present in context"}""")
            .ReturnsAsync("""{"covered":false,"reason":"not mentioned"}""");

        var evaluator = new ContextRecallEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Equal(0.5, outcome.Score);
        Assert.Contains("not mentioned", outcome.Reasoning);
    }

    [Fact]
    public async Task EvaluateAsync_AllClaimsUncovered_ReturnsZeroScore()
    {
        var judge = new Mock<ILlmJudge>();
        judge.SetupSequence(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"claims":["30 day notice period","notice delivered to registered office"]}""")
            .ReturnsAsync("""{"covered":false,"reason":"not mentioned"}""")
            .ReturnsAsync("""{"covered":false,"reason":"not mentioned"}""");

        var evaluator = new ContextRecallEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Equal(0.0, outcome.Score);
    }

    [Fact]
    public async Task EvaluateAsync_UnparseableResponse_ReturnsNullScoreWithoutThrowing()
    {
        var judge = new Mock<ILlmJudge>();
        judge.Setup(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not valid json");

        var evaluator = new ContextRecallEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Null(outcome.Score);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Reasoning));
    }

    [Fact]
    public async Task EvaluateAsync_JudgeCancelled_PropagatesOperationCanceledException()
    {
        var judge = new Mock<ILlmJudge>();
        judge.Setup(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new OperationCanceledException());

        var evaluator = new ContextRecallEvaluator();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => evaluator.EvaluateAsync(CreateInput(), judge.Object));
    }

    [Fact]
    public async Task EvaluateAsync_NoGroundTruth_ReturnsNullScoreWithoutCallingJudge()
    {
        var judge = new Mock<ILlmJudge>();

        var evaluator = new ContextRecallEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(groundTruth: null), judge.Object);

        Assert.Null(outcome.Score);
        judge.Verify(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
