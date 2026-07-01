using RagEval.Export;
using RagEval.Models;
using Xunit;

namespace RagEval.Tests.Export;

public class CsvExporterTests
{
    private const string ExpectedHeader =
        "Question,Answer,Faithfulness,AnswerRelevance,ContextPrecision,ContextRecall," +
        "FaithfulnessReasoning,AnswerRelevanceReasoning,ContextPrecisionReasoning,ContextRecallReasoning";

    private static RagEvaluationResult CreateResult(
        string question = "What is the notice period?",
        string answer = "30 days.",
        double? faithfulness = 0.75,
        double? contextRecall = null) => new()
    {
        Input = new RagEvaluationInput
        {
            Question = question,
            Answer = answer,
            Contexts = ["Clause 12.1: 30 days written notice is required."]
        },
        Faithfulness = faithfulness,
        AnswerRelevance = 0.9,
        ContextPrecision = 1.0,
        ContextRecall = contextRecall,
        Reasoning = new Dictionary<string, string>
        {
            ["Faithfulness"] = "All claims supported."
        }
    };

    private static string GetTempFilePath() =>
        Path.Combine(Path.GetTempPath(), $"rageval-tests-{Guid.NewGuid():N}.csv");

    private static async Task<string[]> ReadLinesAsync(string path)
    {
        string content = await File.ReadAllTextAsync(path);
        return content.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
    }

    [Fact]
    public async Task ExportAsync_WritesExpectedHeaderRow()
    {
        string path = GetTempFilePath();
        try
        {
            IReadOnlyList<RagEvaluationResult> results = [CreateResult()];

            var exporter = new CsvRagEvalExporter();
            await exporter.ExportAsync(results, path);

            string[] lines = await ReadLinesAsync(path);

            Assert.Equal(ExpectedHeader, lines[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_WritesOneDataRowPerResultWithScoresAndReasoning()
    {
        string path = GetTempFilePath();
        try
        {
            IReadOnlyList<RagEvaluationResult> results = [CreateResult(), CreateResult(question: "Second question?")];

            var exporter = new CsvRagEvalExporter();
            await exporter.ExportAsync(results, path);

            string[] lines = await ReadLinesAsync(path);

            Assert.Equal(3, lines.Length);
            Assert.StartsWith("What is the notice period?,30 days.,0.75,0.9,1", lines[1]);
            Assert.Contains("All claims supported.", lines[1]);
            Assert.StartsWith("Second question?", lines[2]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_NullScores_ExportAsEmptyFields()
    {
        string path = GetTempFilePath();
        try
        {
            IReadOnlyList<RagEvaluationResult> results = [CreateResult(faithfulness: null, contextRecall: null)];

            var exporter = new CsvRagEvalExporter();
            await exporter.ExportAsync(results, path);

            string[] lines = await ReadLinesAsync(path);
            string[] fields = lines[1].Split(',');

            Assert.Equal(string.Empty, fields[2]); // Faithfulness
            Assert.Equal(string.Empty, fields[5]); // ContextRecall
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_FieldsContainingCommasAndQuotes_AreEscaped()
    {
        string path = GetTempFilePath();
        try
        {
            IReadOnlyList<RagEvaluationResult> results =
                [CreateResult(answer: "The answer, with a comma and a \"quote\".")];

            var exporter = new CsvRagEvalExporter();
            await exporter.ExportAsync(results, path);

            string content = await File.ReadAllTextAsync(path);

            Assert.Contains("\"The answer, with a comma and a \"\"quote\"\".\"", content);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_MissingDirectory_CreatesDirectoryAndFile()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"rageval-tests-dir-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, "nested", "results.csv");
        try
        {
            IReadOnlyList<RagEvaluationResult> results = [CreateResult()];

            var exporter = new CsvRagEvalExporter();
            await exporter.ExportAsync(results, path);

            Assert.True(File.Exists(path));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ExportAsync_ViaExtensionMethod_WritesCsvWhenFormatSpecified()
    {
        string path = GetTempFilePath();
        try
        {
            IReadOnlyList<RagEvaluationResult> results = [CreateResult()];

            await results.ExportAsync(path, RagEvalExportFormat.Csv);

            string[] lines = await ReadLinesAsync(path);

            Assert.Equal(ExpectedHeader, lines[0]);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
