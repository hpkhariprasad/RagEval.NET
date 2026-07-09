namespace RagEval.Export;

/// <summary>
/// Schema version identifiers for the export formats. Versions follow a major.minor scheme:
/// additive changes bump the minor version, breaking changes bump the major version.
/// </summary>
public static class RagEvalExportSchema
{
    /// <summary>
    /// The JSON document schema version, written to the <c>schemaVersion</c> field of every
    /// <see cref="RagEvalJsonExport"/> document.
    /// </summary>
    public const string JsonSchemaVersion = "1.0";

    /// <summary>
    /// The CSV column-set version. New columns are only ever appended after the existing ones,
    /// and any such addition bumps the minor version.
    /// </summary>
    public const string CsvSchemaVersion = "1.0";
}
