using System.Text.Json;
using RagEval.Judges;
using RagEval.Models;
using RagEval.Prompts;

namespace RagEval.Metrics;

/// <summary>
/// Scores the fraction of factual claims in the answer that are supported by the retrieved
/// contexts, catching hallucinated content.
/// </summary>
public sealed class FaithfulnessEvaluator : IMetricEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <inheritdoc />
    public string MetricName => MetricNames.Faithfulness;

    /// <inheritdoc />
    public async Task<MetricEvaluationOutcome> EvaluateAsync(RagEvaluationInput input, ILlmJudge judge, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(judge);

        List<string> claims;
        try
        {
            string extractionPrompt = string.Format(MetricPrompts.FaithfulnessClaimExtraction, input.Answer);
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
            return new MetricEvaluationOutcome(null, "No factual claims were extracted from the answer.");
        }

        string context = string.Join(MetricPrompts.ContextSeparator, input.Contexts);
        int supportedCount = 0;
        List<string> unsupportedClaims = [];

        foreach (string claim in claims)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string verificationPrompt = string.Format(MetricPrompts.FaithfulnessClaimVerification, claim, context);
                string verificationResponse = await judge.CompleteAsync(MetricPrompts.JsonOnlySystemPrompt, verificationPrompt, ct).ConfigureAwait(false);
                ClaimVerificationResponse? verification = JsonSerializer.Deserialize<ClaimVerificationResponse>(verificationResponse, JsonOptions);

                if (verification is null)
                {
                    return new MetricEvaluationOutcome(null, $"Failed to parse claim verification response for claim: \"{claim}\".");
                }

                if (verification.Supported)
                {
                    supportedCount++;
                }
                else
                {
                    unsupportedClaims.Add($"\"{claim}\" ({verification.Reason})");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new MetricEvaluationOutcome(null, $"Failed to parse claim verification response for claim \"{claim}\": {ex.Message}");
            }
        }

        double score = Math.Clamp((double)supportedCount / claims.Count, 0.0, 1.0);
        string reasoning = unsupportedClaims.Count == 0
            ? $"All {claims.Count} claim(s) extracted from the answer were supported by the retrieved context."
            : $"{unsupportedClaims.Count} of {claims.Count} claim(s) were not supported: {string.Join("; ", unsupportedClaims)}";

        return new MetricEvaluationOutcome(score, reasoning);
    }

    private sealed record ClaimsResponse(List<string> Claims);

    private sealed record ClaimVerificationResponse(bool Supported, string Reason);
}
