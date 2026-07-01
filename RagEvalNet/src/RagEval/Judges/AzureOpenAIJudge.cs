using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace RagEval.Judges;

/// <summary>
/// <see cref="ILlmJudge"/> implementation backed by an Azure OpenAI chat completion deployment.
/// </summary>
public sealed class AzureOpenAIJudge : ILlmJudge
{
    private readonly ChatClient _chatClient;

    /// <summary>
    /// Creates a judge that calls the given Azure OpenAI deployment.
    /// </summary>
    /// <param name="endpoint">The Azure OpenAI resource endpoint, e.g. https://my-resource.openai.azure.com/.</param>
    /// <param name="apiKey">The Azure OpenAI API key.</param>
    /// <param name="deploymentName">The name of the chat completion deployment to use as the judge.</param>
    public AzureOpenAIJudge(string endpoint, string apiKey, string deploymentName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(deploymentName);

        AzureOpenAIClient client = new(new Uri(endpoint), new AzureKeyCredential(apiKey));
        _chatClient = client.GetChatClient(deploymentName);
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
