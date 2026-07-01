namespace RagEval.Judges;

/// <summary>
/// Abstraction over an LLM used as the judge for scoring RAG pipeline output.
/// Implementations must be safe to call concurrently.
/// </summary>
public interface ILlmJudge
{
    /// <summary>
    /// Sends a system/user prompt pair to the judge model and returns the raw text completion.
    /// </summary>
    /// <param name="systemPrompt">Instructions establishing the judge's role and response format.</param>
    /// <param name="userPrompt">The evaluation task, including the expected JSON schema.</param>
    /// <param name="ct">Token used to cancel the in-flight request.</param>
    /// <returns>The raw completion text returned by the model.</returns>
    Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default);
}
