using Moq;
using RagEval.Judges;
using RagEval.Metrics;
using RagEval.Models;
using Xunit;

namespace RagEval.Tests;

public class FaithfulnessTests
{
    private static RagEvaluationInput CreateInput() => new()
    {
        Question = "What is the notice period for termination?",
        Answer = "The notice period is 30 days. It must be given in writing.",
        Contexts = ["Clause 12.1: Either party may terminate with 30 days written notice."]
    };

    [Fact]
    public async Task EvaluateAsync_AllClaimsSupported_ReturnsPerfectScore()
    {
        var judge = new Mock<ILlmJudge>();
        judge.SetupSequence(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"claims":["30 day notice period","notice must be in writing"]}""")
            .ReturnsAsync("""{"supported":true,"reason":"stated in clause 12.1"}""")
            .ReturnsAsync("""{"supported":true,"reason":"stated in clause 12.1"}""");

        var evaluator = new FaithfulnessEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Equal(1.0, outcome.Score);
        Assert.Contains("supported", outcome.Reasoning, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EvaluateAsync_SomeClaimsUnsupported_ReturnsPartialScore()
    {
        var judge = new Mock<ILlmJudge>();
        judge.SetupSequence(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"claims":["30 day notice period","notice must be in writing"]}""")
            .ReturnsAsync("""{"supported":true,"reason":"stated in clause 12.1"}""")
            .ReturnsAsync("""{"supported":false,"reason":"not mentioned in context"}""");

        var evaluator = new FaithfulnessEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Equal(0.5, outcome.Score);
        Assert.Contains("not mentioned in context", outcome.Reasoning);
    }

    [Fact]
    public async Task EvaluateAsync_AllClaimsUnsupported_ReturnsZeroScore()
    {
        var judge = new Mock<ILlmJudge>();
        judge.SetupSequence(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"claims":["30 day notice period","notice must be in writing"]}""")
            .ReturnsAsync("""{"supported":false,"reason":"unsupported"}""")
            .ReturnsAsync("""{"supported":false,"reason":"unsupported"}""");

        var evaluator = new FaithfulnessEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Equal(0.0, outcome.Score);
    }

    [Fact]
    public async Task EvaluateAsync_UnparseableResponse_ReturnsNullScoreWithoutThrowing()
    {
        var judge = new Mock<ILlmJudge>();
        judge.Setup(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("this is not valid json");

        var evaluator = new FaithfulnessEvaluator();

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

        var evaluator = new FaithfulnessEvaluator();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => evaluator.EvaluateAsync(CreateInput(), judge.Object));
    }
}
