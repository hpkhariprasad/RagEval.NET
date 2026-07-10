using RagEval.Models;

namespace RagEval.Export;

/// <summary>
/// The root document written by <see cref="JsonRagEvalExporter"/>. All properties serialize in
/// camelCase. The shape of this document is versioned via <see cref="SchemaVersion"/>: additive
/// changes (new fields) bump the minor version, breaking changes (renamed or removed fields)
/// bump the major version, so downstream consumers such as dashboards can rely on it.
/// </summary>
public sealed class RagEvalJsonExport
{
    /// <summary>
    /// The schema version of this document, currently <see cref="RagEvalExportSchema.JsonSchemaVersion"/>.
    /// </summary>
    public required string SchemaVersion { get; init; }

    /// <summary>The UTC timestamp at which the export was generated (ISO 8601).</summary>
    public required DateTimeOffset GeneratedAt { get; init; }

    /// <summary>Aggregate per-metric averages across <see cref="Results"/>.</summary>
    public required RagEvaluationSummary Summary { get; init; }

    /// <summary>The individual evaluation results, in the order they were supplied.</summary>
    public required IReadOnlyList<RagEvaluationResult> Results { get; init; }
}
