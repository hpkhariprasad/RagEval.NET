using RagEval;
using RagEval.Models;

namespace RagEval.Samples;

/// <summary>
/// Minimal end-to-end example: score a single RAG output, then a batch, using Azure OpenAI as the judge.
/// </summary>
public static class BasicEvaluation
{
    public static async Task Main(string[] args)
    {
        string endpoint = Environment.GetEnvironmentVariable("AZURE_OPENAI_ENDPOINT")
            ?? throw new InvalidOperationException("Set the AZURE_OPENAI_ENDPOINT environment variable.");
        string apiKey = Environment.GetEnvironmentVariable("AZURE_OPENAI_API_KEY")
            ?? throw new InvalidOperationException("Set the AZURE_OPENAI_API_KEY environment variable.");
        string deploymentName = Environment.GetEnvironmentVariable("AZURE_OPENAI_DEPLOYMENT")
            ?? throw new InvalidOperationException("Set the AZURE_OPENAI_DEPLOYMENT environment variable.");

        RagEvaluator evaluator = new RagEvaluatorBuilder()
            .UseAzureOpenAI(endpoint, apiKey, deploymentName)
            .WithJudgeModel("gpt-4o")
            .WithMaxConcurrency(5)
            .Build();

        RagEvaluationInput input = new()
        {
            Question = "What is the notice period for termination?",
            Answer = "The notice period is 30 days.",
            Contexts =
            [
                "Clause 12.1: Either party may terminate this agreement by giving 30 days written notice.",
                "Clause 12.2: Notice must be delivered to the registered office of the other party."
            ],
            GroundTruth = "30 days written notice per clause 12.1"
        };

        RagEvaluationResult result = await evaluator.EvaluateAsync(input);
        PrintResult(result);

        List<RagEvaluationInput> batch = [input, input];

        IReadOnlyList<RagEvaluationResult> results = await evaluator.EvaluateBatchAsync(batch);
        RagEvaluationSummary summary = results.GetSummary();

        Console.WriteLine();
        Console.WriteLine("Batch summary:");
        Console.WriteLine($"  Total evaluated:      {summary.TotalEvaluated}");
        Console.WriteLine($"  Avg Faithfulness:     {summary.AvgFaithfulness:F2}");
        Console.WriteLine($"  Avg Answer Relevance: {summary.AvgAnswerRelevance:F2}");
        Console.WriteLine($"  Avg Context Precision:{summary.AvgContextPrecision:F2}");
        Console.WriteLine($"  Avg Context Recall:   {summary.AvgContextRecall:F2}");
    }

    private static void PrintResult(RagEvaluationResult result)
    {
        Console.WriteLine("Single evaluation:");
        Console.WriteLine($"  Faithfulness:      {result.Faithfulness:F2}");
        Console.WriteLine($"  Answer Relevance:  {result.AnswerRelevance:F2}");
        Console.WriteLine($"  Context Precision: {result.ContextPrecision:F2}");
        Console.WriteLine($"  Context Recall:    {result.ContextRecall:F2}");

        foreach ((string metric, string reasoning) in result.Reasoning)
        {
            Console.WriteLine($"  [{metric}] {reasoning}");
        }
    }
}
