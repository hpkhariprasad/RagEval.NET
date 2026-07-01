using Moq;
using RagEval.Judges;
using RagEval.Metrics;
using RagEval.Models;
using Xunit;

namespace RagEval.Tests;

public class AnswerRelevanceTests
{
    private static RagEvaluationInput CreateInput() => new()
    {
        Question = "What is the notice period for termination?",
        Answer = "The notice period is 30 days.",
        Contexts = ["Clause 12.1: Either party may terminate with 30 days written notice."]
    };

    [Fact]
    public async Task EvaluateAsync_AllReverseQuestionsHighlySimilar_ReturnsPerfectScore()
    {
        var judge = new Mock<ILlmJudge>();
        judge.SetupSequence(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"questions":["What is the termination notice period?","How many days notice?","What notice is required?"]}""")
            .ReturnsAsync("""{"similarity":1.0}""")
            .ReturnsAsync("""{"similarity":1.0}""")
            .ReturnsAsync("""{"similarity":1.0}""");

        var evaluator = new AnswerRelevanceEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Equal(1.0, outcome.Score);
    }

    [Fact]
    public async Task EvaluateAsync_MixedSimilarityScores_ReturnsAverage()
    {
        var judge = new Mock<ILlmJudge>();
        judge.SetupSequence(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"questions":["q1","q2","q3"]}""")
            .ReturnsAsync("""{"similarity":1.0}""")
            .ReturnsAsync("""{"similarity":0.5}""")
            .ReturnsAsync("""{"similarity":0.0}""");

        var evaluator = new AnswerRelevanceEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Equal(0.5, outcome.Score);
    }

    [Fact]
    public async Task EvaluateAsync_AllReverseQuestionsUnrelated_ReturnsZeroScore()
    {
        var judge = new Mock<ILlmJudge>();
        judge.SetupSequence(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("""{"questions":["q1","q2","q3"]}""")
            .ReturnsAsync("""{"similarity":0.0}""")
            .ReturnsAsync("""{"similarity":0.0}""")
            .ReturnsAsync("""{"similarity":0.0}""");

        var evaluator = new AnswerRelevanceEvaluator();

        MetricEvaluationOutcome outcome = await evaluator.EvaluateAsync(CreateInput(), judge.Object);

        Assert.Equal(0.0, outcome.Score);
    }

    [Fact]
    public async Task EvaluateAsync_UnparseableResponse_ReturnsNullScoreWithoutThrowing()
    {
        var judge = new Mock<ILlmJudge>();
        judge.Setup(j => j.CompleteAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("not json at all");

        var evaluator = new AnswerRelevanceEvaluator();

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

        var evaluator = new AnswerRelevanceEvaluator();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => evaluator.EvaluateAsync(CreateInput(), judge.Object));
    }
}
