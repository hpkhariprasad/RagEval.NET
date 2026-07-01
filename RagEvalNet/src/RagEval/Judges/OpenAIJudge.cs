using OpenAI;
using OpenAI.Chat;

namespace RagEval.Judges;

/// <summary>
/// <see cref="ILlmJudge"/> implementation backed by the standard OpenAI API.
/// </summary>
public sealed class OpenAIJudge : ILlmJudge
{
    private readonly ChatClient _chatClient;

    /// <summary>
    /// Creates a judge that calls the OpenAI chat completions API.
    /// </summary>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="model">The model to use as the judge, e.g. "gpt-4o".</param>
    public OpenAIJudge(string apiKey, string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        OpenAIClient client = new(apiKey);
        _chatClient = client.GetChatClient(model);
    }

    /// <inheritdoc />
    public async Task<string> CompleteAsync(string systemPrompt, string userPrompt, CancellationToken ct = default)
    {
        List<ChatMessage> messages =
        [
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        ];

        ChatCompletion completion = await _chatClient.CompleteChatAsync(messages, cancellationToken: ct).ConfigureAwait(false);
        return completion.Content.Count > 0 ? completion.Content[0].Text : string.Empty;
    }
}
