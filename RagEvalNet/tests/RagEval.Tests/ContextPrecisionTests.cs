using Moq;
using RagEval.Judges;
using RagEval.Metrics;
using RagEval.Models;
using Xunit;

namespace RagEval.Tests;

public class ContextPrecisionTests
{
    private static RagEvaluationInput CreateInput() => new()
    {
        Question = "What is the notice period for termination?",
        Answer = "The notice period is 30 days.",
        Contexts = ["Clause 12.1: 30 days written notice is required.", "Clause 4.2: Payment terms are net 30."]
    };

    [Fact]
    public async Task EvaluateAsync_AllChunksUseful_ReturnsPerfectScore()
    {
        var judge = new Mock<ILlmJudge>();
        judge.SetupSequence(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"useful":true,"reason":"directly answers the question"}""")
            .ReturnsAsync("""{"useful":true,"reason":"directly answers the question"}""");

        var evaluator = new ContextPrecisionEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Equal(1.0, outcome.Score);
    }

    [Fact]
    public async Task EvaluateAsync_SomeChunksNoisy_ReturnsPartialScore()
    {
        var judge = new Mock<ILlmJudge>();
        judge.SetupSequence(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"useful":true,"reason":"directly answers the question"}""")
            .ReturnsAsync("""{"useful":false,"reason":"unrelated to termination notice"}""");

        var evaluator = new ContextPrecisionEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Equal(0.5, outcome.Score);
        Assert.Contains("noise", outcome.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_AllChunksNoisy_ReturnsZeroScore()
    {
        var judge = new Mock<ILlmJudge>();
        judge.SetupSequence(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"useful":false,"reason":"unrelated"}""")
            .ReturnsAsync("""{"useful":false,"reason":"unrelated"}""");

        var evaluator = new ContextPrecisionEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Equal(0.0, outcome.Score);
    }

    [Fact]
    public async Task EvaluateAsync_UnparseableResponse_ReturnsNullScoreWithoutThrowing()
    {
        var judge = new Mock<ILlmJudge>();
        judge.Setup(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("garbage response");

        var evaluator = new ContextPrecisionEvaluator();

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

        var evaluator = new ContextPrecisionEvaluator();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => evaluator.EvaluateAsync(CreateInput(), judge.Object));
    }
}
