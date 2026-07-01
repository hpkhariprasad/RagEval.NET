using RagEval.Models;

namespace RagEval.Export;

/// <summary>Extension methods for exporting batches of evaluation results to a file.</summary>
public static class RagEvalExportExtensions
{
    /// <summary>
    /// Exports the given results to <paramref name="filePath"/> in the requested <paramref name="format"/>.
    /// </summary>
    /// <param name="results">The results to export.</param>
    /// <param name="filePath">The destination file path.</param>
    /// <param name="format">The export format. Defaults to <see cref="RagEvalExportFormat.Json"/>.</param>
    /// <param name="ct">Token used to cancel the export.</param>
    public static Task ExportAsync(
        this IReadOnlyList<RagEvaluationResult> results,
        string filePath,
        RagEvalExportFormat format = RagEvalExportFormat.Json,
        CancellationToken ct = default)
    {
        IRagEvalExporter exporter = format switch
        {
            RagEvalExportFormat.Json => new JsonRagEvalExporter(),
            RagEvalExportFormat.Csv => new CsvRagEvalExporter(),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unsupported export format.")
        };

        return exporter.ExportAsync(results, filePath, ct);
    }
}
