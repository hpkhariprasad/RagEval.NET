using System.Text.Json;
using RagEval.Judges;
using RagEval.Models;
using RagEval.Prompts;

namespace RagEval.Metrics;

/// <summary>
/// Scores how relevant the answer is to the question by generating reverse-engineered questions
/// from the answer and measuring their similarity to the original question, catching off-topic
/// or padded answers.
/// </summary>
public sealed class AnswerRelevanceEvaluator : IMetricEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <inheritdoc />
    public string MetricName => MetricNames.AnswerRelevance;

    /// <inheritdoc />
    public async Task<MetricEvaluationOutcome> EvaluateAsync(RagEvaluationInput input, ILlmJudge judge, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(judge);

        List<string> reverseQuestions;
        try
        {
            string generationPrompt = string.Format(MetricPrompts.AnswerRelevanceReverseQuestion, input.Answer);
            string generationResponse = await judge.CompleteAsync(MetricPrompts.JsonOnlySystemPrompt, generationPrompt, ct).ConfigureAwait(false);
            ReverseQuestionsResponse? parsed = JsonSerializer.Deserialize<ReverseQuestionsResponse>(generationResponse, JsonOptions);
            reverseQuestions = parsed?.Questions ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new MetricEvaluationOutcome(null, $"Failed to parse reverse-question generation response: {ex.Message}");
        }

        if (reverseQuestions.Count == 0)
        {
            return new MetricEvaluationOutcome(null, "No reverse questions could be generated from the answer.");
        }

        List<double> similarities = [];
        List<string> details = [];

        foreach (string reverseQuestion in reverseQuestions)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string scoringPrompt = string.Format(MetricPrompts.AnswerRelevanceSimilarityScore, input.Question, reverseQuestion);
                string scoringResponse = await judge.CompleteAsync(MetricPrompts.JsonOnlySystemPrompt, scoringPrompt, ct).ConfigureAwait(false);
                SimilarityResponse? similarity = JsonSerializer.Deserialize<SimilarityResponse>(scoringResponse, JsonOptions);

                if (similarity is null)
                {
                    return new MetricEvaluationOutcome(null, $"Failed to parse similarity response for reverse question: \"{reverseQuestion}\".");
                }

                double clamped = Math.Clamp(similarity.Similarity, 0.0, 1.0);
                similarities.Add(clamped);
                details.Add($"\"{reverseQuestion}\" (similarity: {clamped:F2})");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new MetricEvaluationOutcome(null, $"Failed to parse similarity response for reverse question \"{reverseQuestion}\": {ex.Message}");
            }
        }

        double score = Math.Clamp(similarities.Average(), 0.0, 1.0);
        string reasoning = $"Generated reverse questions: {string.Join("; ", details)}";

        return new MetricEvaluationOutcome(score, reasoning);
    }

    private sealed record ReverseQuestionsResponse(List<string> Questions);

    private sealed record SimilarityResponse(double Similarity);
}
