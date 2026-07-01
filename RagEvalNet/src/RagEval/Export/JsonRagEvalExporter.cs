using System.Text.Json;
using RagEval.Models;

namespace RagEval.Export;

/// <summary>
/// Exports batch evaluation results as a pretty-printed JSON array using <see cref="JsonSerializer"/>.
/// Null metric scores are serialized as JSON <c>null</c>.
/// </summary>
public sealed class JsonRagEvalExporter : IRagEvalExporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true
    };

    /// <inheritdoc />
    public async Task ExportAsync(IReadOnlyList<RagEvaluationResult> results, string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        RagEvalExportFileHelper.EnsureDirectoryExists(filePath);

        string json = JsonSerializer.Serialize(results, SerializerOptions);

        await using StreamWriter writer = new(filePath, append: false);
        await writer.WriteAsync(json.AsMemory(), ct).ConfigureAwait(false);
    }
}
