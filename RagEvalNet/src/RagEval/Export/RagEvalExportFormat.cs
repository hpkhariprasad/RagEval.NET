namespace RagEval.Export;

/// <summary>The file format to export batch evaluation results to.</summary>
public enum RagEvalExportFormat
{
    /// <summary>A pretty-printed JSON array of results.</summary>
    Json,

    /// <summary>Comma-separated values, with one row per result.</summary>
    Csv
}
