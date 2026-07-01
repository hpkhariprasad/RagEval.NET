using System.Text.Json;
using RagEval.Judges;
using RagEval.Models;
using RagEval.Prompts;

namespace RagEval.Metrics;

/// <summary>
/// Scores the fraction of ground-truth claims that are covered by the retrieved contexts,
/// catching incomplete retrieval. Requires <see cref="RagEvaluationInput.GroundTruth"/> to be set.
/// </summary>
public sealed class ContextRecallEvaluator : IMetricEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <inheritdoc />
    public string MetricName => MetricNames.ContextRecall;

    /// <inheritdoc />
    public async Task<MetricEvaluationOutcome> EvaluateAsync(RagEvaluationInput input, ILlmJudge judge, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(judge);

        if (string.IsNullOrWhiteSpace(input.GroundTruth))
        {
            return new MetricEvaluationOutcome(null, "GroundTruth was not provided; context recall was not evaluated.");
        }

        List<string> claims;
        try
        {
            string extractionPrompt = string.Format(MetricPrompts.ContextRecallClaimExtraction, input.GroundTruth);
            string extractionResponse = await judge.CompleteAsync(MetricPrompts.JsonOnlySystemPrompt, extractionPrompt, ct).ConfigureAwait(false);
            ClaimsResponse? parsed = JsonSerializer.Deserialize<ClaimsResponse>(extractionResponse, JsonOptions);
            claims = parsed?.Claims ?? [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new MetricEvaluationOutcome(null, $"Failed to parse claim extraction response: {ex.Message}");
        }

        if (claims.Count == 0)
        {
            return new MetricEvaluationOutcome(null, "No factual claims were extracted from the ground truth.");
        }

        string context = string.Join(MetricPrompts.ContextSeparator, input.Contexts);
        int coveredCount = 0;
        List<string> uncoveredClaims = [];

        foreach (string claim in claims)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string coveragePrompt = string.Format(MetricPrompts.ContextRecallClaimCoverage, claim, context);
                string coverageResponse = await judge.CompleteAsync(MetricPrompts.JsonOnlySystemPrompt, coveragePrompt, ct).ConfigureAwait(false);
                ClaimCoverageResponse? coverage = JsonSerializer.Deserialize<ClaimCoverageResponse>(coverageResponse, JsonOptions);

                if (coverage is null)
                {
                    return new MetricEvaluationOutcome(null, $"Failed to parse claim coverage response for claim: \"{claim}\".");
                }

                if (coverage.Covered)
                {
                    coveredCount++;
                }
                else
                {
                    uncoveredClaims.Add($"\"{claim}\" ({coverage.Reason})");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new MetricEvaluationOutcome(null, $"Failed to parse claim coverage response for claim \"{claim}\": {ex.Message}");
            }
        }

        double score = Math.Clamp((double)coveredCount / claims.Count, 0.0, 1.0);
        string reasoning = uncoveredClaims.Count == 0
            ? $"All {claims.Count} ground-truth claim(s) were covered by the retrieved context."
            : $"{uncoveredClaims.Count} of {claims.Count} ground-truth claim(s) were not covered: {string.Join("; ", uncoveredClaims)}";

        return new MetricEvaluationOutcome(score, reasoning);
    }

    private sealed record ClaimsResponse(List<string> Claims);

    private sealed record ClaimCoverageResponse(bool Covered, string Reason);
}
