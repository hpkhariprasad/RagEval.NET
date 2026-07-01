namespace RagEval.Export;

/// <summary>Shared file-system helpers used by <see cref="IRagEvalExporter"/> implementations.</summary>
internal static class RagEvalExportFileHelper
{
    /// <summary>Creates the parent directory of <paramref name="filePath"/> if it does not already exist.</summary>
    public static void EnsureDirectoryExists(string filePath)
    {
        string? directory = Path.GetDirectoryName(Path.GetFullPath(filePath));
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
