using RagEval.Judges;
using RagEval.Metrics;

namespace RagEval;

/// <summary>
/// Fluent builder for configuring and constructing a <see cref="RagEvaluator"/>.
/// </summary>
public sealed class RagEvaluatorBuilder
{
    private ILlmJudge? _judge;
    private string? _judgeModel;
    private int _maxConcurrency = 3;

    /// <summary>Configures the evaluator to use Azure OpenAI as the judge LLM.</summary>
    /// <param name="endpoint">The Azure OpenAI resource endpoint, e.g. https://my-resource.openai.azure.com/.</param>
    /// <param name="apiKey">The Azure OpenAI API key.</param>
    /// <param name="deploymentName">The name of the chat completion deployment to use as the judge.</param>
    public RagEvaluatorBuilder UseAzureOpenAI(string endpoint, string apiKey, string deploymentName)
    {
        _judge = new AzureOpenAIJudge(endpoint, apiKey, deploymentName);
        return this;
    }

    /// <summary>Configures the evaluator to use the standard OpenAI API as the judge LLM.</summary>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="model">The model to use as the judge, e.g. "gpt-4o".</param>
    public RagEvaluatorBuilder UseOpenAI(string apiKey, string model)
    {
        _judge = new OpenAIJudge(apiKey, model);
        _judgeModel ??= model;
        return this;
    }

    /// <summary>Configures the evaluator to use a custom <see cref="ILlmJudge"/> implementation.</summary>
    public RagEvaluatorBuilder UseJudge(ILlmJudge judge)
    {
        _judge = judge ?? throw new ArgumentNullException(nameof(judge));
        return this;
    }

    /// <summary>
    /// Records the underlying judge model name for diagnostics, exposed via <see cref="RagEvaluator.JudgeModel"/>.
    /// Useful with Azure OpenAI, where the deployment name passed to <see cref="UseAzureOpenAI"/> may differ
    /// from the underlying model name.
    /// </summary>
    public RagEvaluatorBuilder WithJudgeModel(string modelName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelName);
        _judgeModel = modelName;
        return this;
    }

    /// <summary>Sets the maximum number of concurrent judge calls made during batch evaluation. Defaults to 3.</summary>
    public RagEvaluatorBuilder WithMaxConcurrency(int maxConcurrency)
    {
        if (maxConcurrency < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), maxConcurrency, "Max concurrency must be at least 1.");
        }

        _maxConcurrency = maxConcurrency;
        return this;
    }

    /// <summary>Builds the configured <see cref="RagEvaluator"/>.</summary>
    /// <exception cref="InvalidOperationException">No judge was configured.</exception>
    public RagEvaluator Build()
    {
        if (_judge is null)
        {
            throw new InvalidOperationException(
                "A judge must be configured before building a RagEvaluator. Call UseAzureOpenAI, UseOpenAI, or UseJudge first.");
        }

        IReadOnlyList<IMetricEvaluator> metricEvaluators =
        [
            new FaithfulnessEvaluator(),
            new AnswerRelevanceEvaluator(),
            new ContextPrecisionEvaluator(),
            new ContextRecallEvaluator()
        ];

        return new RagEvaluator(_judge, metricEvaluators, _maxConcurrency, _judgeModel);
    }
}
