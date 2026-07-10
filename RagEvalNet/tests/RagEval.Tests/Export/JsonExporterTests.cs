using System.Text.Json;
using RagEval.Export;
using RagEval.Models;
using Xunit;

namespace RagEval.Tests.Export;

public class JsonExporterTests
{
    private static RagEvaluationResult CreateResult(double? faithfulness = 0.75) => new()
    {
        Input = new RagEvaluationInput
        {
            Question = "What is the notice period?",
            Answer = "30 days.",
            Contexts = ["Clause 12.1: 30 days written notice is required."]
        },
        Faithfulness = faithfulness,
        AnswerRelevance = 0.9,
        ContextPrecision = 1.0,
        ContextRecall = null,
        Reasoning = new Dictionary<string, string>
        {
            ["Faithfulness"] = "All claims supported."
        }
    };

    private static string GetTempFilePath(string extension) =>
        Path.Combine(Path.GetTempPath(), $"rageval-tests-{Guid.NewGuid():N}.{extension}");

    [Fact]
    public async Task ExportAsync_WritesVersionedEnvelopeWithResultProperties()
    {
        string path = GetTempFilePath("json");
        try
        {
            IReadOnlyList<RagEvaluationResult> results = [CreateResult()];

            var exporter = new JsonRagEvalExporter();
            await exporter.ExportAsync(results, path);

            string json = await File.ReadAllTextAsync(path);
            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement root = document.RootElement;
            Assert.Equal(RagEvalExportSchema.JsonSchemaVersion, root.GetProperty("schemaVersion").GetString());
            Assert.True(root.TryGetProperty("generatedAt", out JsonElement generatedAt));
            Assert.NotEqual(default, generatedAt.GetDateTimeOffset());

            JsonElement resultsElement = root.GetProperty("results");
            Assert.Equal(JsonValueKind.Array, resultsElement.ValueKind);
            Assert.Equal(1, resultsElement.GetArrayLength());

            JsonElement first = resultsElement[0];
            Assert.Equal(0.75, first.GetProperty("faithfulness").GetDouble());
            Assert.Equal(0.9, first.GetProperty("answerRelevance").GetDouble());
            Assert.Equal("30 days.", first.GetProperty("input").GetProperty("answer").GetString());
            Assert.Equal("All claims supported.", first.GetProperty("reasoning").GetProperty("Faithfulness").GetString());
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_WritesSummaryAveragedAcrossResults()
    {
        string path = GetTempFilePath("json");
        try
        {
            IReadOnlyList<RagEvaluationResult> results = [CreateResult(faithfulness: 0.5), CreateResult(faithfulness: 1.0)];

            var exporter = new JsonRagEvalExporter();
            await exporter.ExportAsync(results, path);

            string json = await File.ReadAllTextAsync(path);
            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement summary = document.RootElement.GetProperty("summary");
            Assert.Equal(0.75, summary.GetProperty("avgFaithfulness").GetDouble());
            Assert.Equal(2, summary.GetProperty("totalEvaluated").GetInt32());
            Assert.Equal(JsonValueKind.Null, summary.GetProperty("avgContextRecall").ValueKind);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ExportAsync_NullScore_SerializedAsJsonNull()
    {
        string path = GetTempFilePath("json");
        try
        {
            IReadOnlyList<RagEvaluationResult> results = [CreateResult(faithfulness: null)];

            var exporter = new JsonRagEvalExporter();
            await exporter.ExportAsync(results, path);

            string json = await File.ReadAllTextAsync(path);
            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement first = document.RootElement.GetProperty("results")[0];

            Assert.Equal(JsonValueKind.Null, first.GetProperty("contextRecall").ValueKind);
            Assert.Equal(JsonValueKind.Null, first.GetProperty("faithfulness").ValueKind);
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
        string path = Path.Combine(directory, "nested", "results.json");
        try
        {
            IReadOnlyList<RagEvaluationResult> results = [CreateResult()];

            var exporter = new JsonRagEvalExporter();
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
    public async Task ExportAsync_ViaExtensionMethod_DefaultsToJson()
    {
        string path = GetTempFilePath("json");
        try
        {
            IReadOnlyList<RagEvaluationResult> results = [CreateResult()];

            await results.ExportAsync(path);

            string json = await File.ReadAllTextAsync(path);
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.True(document.RootElement.TryGetProperty("schemaVersion", out _));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
