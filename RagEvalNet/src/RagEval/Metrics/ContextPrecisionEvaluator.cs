using System.Text.Json;
using RagEval.Judges;
using RagEval.Models;
using RagEval.Prompts;

namespace RagEval.Metrics;

/// <summary>
/// Scores the fraction of retrieved context chunks that actually contributed to the answer,
/// catching poor retrieval quality (noisy or irrelevant chunks).
/// </summary>
public sealed class ContextPrecisionEvaluator : IMetricEvaluator
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <inheritdoc />
    public string MetricName => MetricNames.ContextPrecision;

    /// <inheritdoc />
    public async Task<MetricEvaluationOutcome> EvaluateAsync(RagEvaluationInput input, ILlmJudge judge, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(judge);

        if (input.Contexts.Count == 0)
        {
            return new MetricEvaluationOutcome(null, "No context chunks were provided.");
        }

        int usefulCount = 0;
        List<string> noiseChunks = [];

        foreach (string chunk in input.Contexts)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                string prompt = string.Format(MetricPrompts.ContextPrecisionChunkUsefulness, input.Question, chunk);
                string response = await judge.CompleteAsync(MetricPrompts.JsonOnlySystemPrompt, prompt, ct).ConfigureAwait(false);
                ChunkUsefulnessResponse? usefulness = JsonSerializer.Deserialize<ChunkUsefulnessResponse>(response, JsonOptions);

                if (usefulness is null)
                {
                    return new MetricEvaluationOutcome(null, $"Failed to parse chunk usefulness response for chunk: \"{Truncate(chunk)}\".");
                }

                if (usefulness.Useful)
                {
                    usefulCount++;
                }
                else
                {
                    noiseChunks.Add($"\"{Truncate(chunk)}\" ({usefulness.Reason})");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new MetricEvaluationOutcome(null, $"Failed to parse chunk usefulness response for chunk \"{Truncate(chunk)}\": {ex.Message}");
            }
        }

        double score = Math.Clamp((double)usefulCount / input.Contexts.Count, 0.0, 1.0);
        string reasoning = noiseChunks.Count == 0
            ? $"All {input.Contexts.Count} context chunk(s) contributed to the answer."
            : $"{noiseChunks.Count} of {input.Contexts.Count} chunk(s) were noise: {string.Join("; ", noiseChunks)}";

        return new MetricEvaluationOutcome(score, reasoning);
    }

    private static string Truncate(string text) => text.Length <= 80 ? text : string.Concat(text.AsSpan(0, 80), "...");

    private sealed record ChunkUsefulnessResponse(bool Useful, string Reason);
}
