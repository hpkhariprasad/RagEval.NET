using RagEval.Models;

namespace RagEval.Export;

/// <summary>
/// Exports a batch of evaluation results to a file in a specific format.
/// </summary>
public interface IRagEvalExporter
{
    /// <summary>
    /// Writes <paramref name="results"/> to <paramref name="filePath"/>, creating the parent
    /// directory first if it does not already exist.
    /// </summary>
    /// <param name="results">The results to export.</param>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="ct">Token used to cancel the export.</param>
    Task ExportAsync(IReadOnlyList<RagEvaluationResult> results, string filePath, CancellationToken ct = default);
}
