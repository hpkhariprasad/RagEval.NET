using System.Globalization;
using System.Text;
using RagEval.Metrics;
using RagEval.Models;

namespace RagEval.Export;

/// <summary>
/// Exports batch evaluation results as CSV, with one row per result. Null metric scores are
/// exported as an empty field. The column set is versioned via
/// <see cref="RagEvalExportSchema.CsvSchemaVersion"/>: new columns are only ever appended after
/// the existing ones, so column positions are stable for downstream consumers.
/// </summary>
public sealed class CsvRagEvalExporter : IRagEvalExporter
{
    private const string Header =
        "Question,Answer,Faithfulness,AnswerRelevance,ContextPrecision,ContextRecall," +
        "FaithfulnessReasoning,AnswerRelevanceReasoning,ContextPrecisionReasoning,ContextRecallReasoning";

    /// <inheritdoc />
    public async Task ExportAsync(IReadOnlyList<RagEvaluationResult> results, string filePath, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(results);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        RagEvalExportFileHelper.EnsureDirectoryExists(filePath);

        StringBuilder builder = new();
        builder.Append(Header).Append('\r').Append('\n');

        foreach (RagEvaluationResult result in results)
        {
            ct.ThrowIfCancellationRequested();
            AppendRow(builder, result);
        }

        await using StreamWriter writer = new(filePath, append: false);
        await writer.WriteAsync(builder.ToString().AsMemory(), ct).ConfigureAwait(false);
    }

    private static void AppendRow(StringBuilder builder, RagEvaluationResult result)
    {
        string[] fields =
        [
            EscapeField(result.Input.Question),
            EscapeField(result.Input.Answer),
            FormatScore(result.Faithfulness),
            FormatScore(result.AnswerRelevance),
            FormatScore(result.ContextPrecision),
            FormatScore(result.ContextRecall),
            EscapeField(GetReasoning(result, MetricNames.Faithfulness)),
            EscapeField(GetReasoning(result, MetricNames.AnswerRelevance)),
            EscapeField(GetReasoning(result, MetricNames.ContextPrecision)),
            EscapeField(GetReasoning(result, MetricNames.ContextRecall))
        ];

        builder.Append(string.Join(',', fields)).Append('\r').Append('\n');
    }

    private static string FormatScore(double? score) =>
        score?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;

    private static string GetReasoning(RagEvaluationResult result, string metricName) =>
        result.Reasoning.TryGetValue(metricName, out string? reasoning) ? reasoning : string.Empty;

    private static string EscapeField(string value)
    {
        if (value.IndexOfAny([',', '"', '\r', '\n']) < 0)
        {
            return value;
        }

        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}
