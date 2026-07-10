using System.Text.Json;
using RagEval.Models;

namespace RagEval.Export;

/// <summary>
/// Exports batch evaluation results as a pretty-printed, camelCase <see cref="RagEvalJsonExport"/>
/// document carrying a <c>schemaVersion</c> field, a generation timestamp, aggregate summary
/// scores, and the individual results. Null metric scores are serialized as JSON <c>null</c>.
/// </summary>
public sealed class JsonRagEvalExporter : IRagEvalExporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    /// <inheritdoc />
    public async Task ExportAsync(IReadOnlyList<RagEvaluationResult> results, string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        RagEvalExportFileHelper.EnsureDirectoryExists(filePath);

        var export = new RagEvalJsonExport
        {
            SchemaVersion = RagEvalExportSchema.JsonSchemaVersion,
            GeneratedAt = DateTimeOffset.UtcNow,
            Summary = results.GetSummary(),
            Results = results
        };

        string json = JsonSerializer.Serialize(export, SerializerOptions);

        await using StreamWriter writer = new(filePath, append: false);
        await writer.WriteAsync(json.AsMemory(), ct).ConfigureAwait(false);
    }
}
