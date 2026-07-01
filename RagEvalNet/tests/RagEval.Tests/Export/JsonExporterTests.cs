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
    public async Task ExportAsync_WritesJsonArrayWithResultProperties()
    {
        string path = GetTempFilePath("json");
        try
        {
            IReadOnlyList<RagEvaluationResult> results = [CreateResult()];

            var exporter = new JsonRagEvalExporter();
            await exporter.ExportAsync(results, path);

            string json = await File.ReadAllTextAsync(path);
            using JsonDocument document = JsonDocument.Parse(json);

            Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
            Assert.Equal(1, document.RootElement.GetArrayLength());

            JsonElement first = document.RootElement[0];
            Assert.Equal(0.75, first.GetProperty("Faithfulness").GetDouble());
            Assert.Equal(0.9, first.GetProperty("AnswerRelevance").GetDouble());
            Assert.Equal("30 days.", first.GetProperty("Input").GetProperty("Answer").GetString());
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

            JsonElement contextRecall = document.RootElement[0].GetProperty("ContextRecall");
            JsonElement faithfulness = document.RootElement[0].GetProperty("Faithfulness");

            Assert.Equal(JsonValueKind.Null, contextRecall.ValueKind);
            Assert.Equal(JsonValueKind.Null, faithfulness.ValueKind);
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

            Assert.Equal(JsonValueKind.Array, document.RootElement.ValueKind);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
