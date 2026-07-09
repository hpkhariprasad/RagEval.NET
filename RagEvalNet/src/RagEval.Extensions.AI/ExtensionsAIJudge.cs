using Microsoft.Extensions.AI;
using RagEval.Judges;

namespace RagEval.Extensions.AI;

/// <summary>
/// <see cref="ILlmJudge"/> implementation backed by any Microsoft.Extensions.AI <see cref="IChatClient"/>,
/// allowing RagEval.NET to use Anthropic Claude, Azure OpenAI, OpenAI, Ollama, Mistral, or any other
/// provider that has an <see cref="IChatClient"/> implementation as the evaluation judge.
/// </summary>
public sealed class ExtensionsAIJudge : ILlmJudge
{
    private readonly IChatClient _chatClient;

    /// <summary>Creates a judge that delegates completions to the given <see cref="IChatClient"/>.</summary>
    /// <param name="chatClient">The chat client to use as the judge.</param>
    public ExtensionsAIJudge(IChatClient chatClient)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        _chatClient = chatClient;
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        List<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userPrompt)
        ];

        ChatResponse response = await _chatClient.GetResponseAsync(messages, cancellationToken: ct).ConfigureAwait(false);
        return response.Text;
    }
}
