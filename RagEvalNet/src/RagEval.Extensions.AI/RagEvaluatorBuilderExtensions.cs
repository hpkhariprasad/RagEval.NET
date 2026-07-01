using Microsoft.Extensions.AI;

namespace RagEval.Extensions.AI;

/// <summary>Extension methods for configuring a <see cref="RagEvaluatorBuilder"/> from Microsoft.Extensions.AI.</summary>
public static class RagEvaluatorBuilderExtensions
{
    /// <summary>
    /// Configures the evaluator to use any Microsoft.Extensions.AI <see cref="IChatClient"/> as the judge LLM,
    /// enabling providers such as Ollama, Mistral, Azure OpenAI, or OpenAI through a single interface.
    /// </summary>
    /// <param name="builder">The builder to configure.</param>
    /// <param name="chatClient">The chat client to use as the judge.</param>
    public static RagEvaluatorBuilder UseExtensionsAI(this RagEvaluatorBuilder builder, IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(chatClient);

        return builder.UseJudge(new ExtensionsAIJudge(chatClient));
    }
}
