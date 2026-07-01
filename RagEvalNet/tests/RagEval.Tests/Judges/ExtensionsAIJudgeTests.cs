using Microsoft.Extensions.AI;
using Moq;
using RagEval.Extensions.AI;
using Xunit;

namespace RagEval.Tests.Judges;

public class ExtensionsAIJudgeTests
{
    [Fact]
    public async Task CompleteAsync_PassesSystemAndUserPromptsAsSeparateChatMessages()
    {
        List<ChatMessage>? capturedMessages = null;

        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((messages, _, _) => capturedMessages = messages.ToList())
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "the answer")));

        var judge = new ExtensionsAIJudge(chatClient.Object);

        await judge.CompleteAsync("system instructions", "user question");

        Assert.NotNull(capturedMessages);
        Assert.Equal(2, capturedMessages!.Count);
        Assert.Equal(ChatRole.System, capturedMessages[0].Role);
        Assert.Equal("system instructions", capturedMessages[0].Text);
        Assert.Equal(ChatRole.User, capturedMessages[1].Role);
        Assert.Equal("user question", capturedMessages[1].Text);
    }

    [Fact]
    public async Task CompleteAsync_ReturnsResponseText()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, """{"claims":[]}""")));

        var judge = new ExtensionsAIJudge(chatClient.Object);

        string result = await judge.CompleteAsync("system", "user");

        Assert.Equal("""{"claims":[]}""", result);
    }

    [Fact]
    public async Task CompleteAsync_ForwardsCancellationToken()
    {
        using CancellationTokenSource cts = new();
        CancellationToken capturedToken = default;

        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .Callback<IEnumerable<ChatMessage>, ChatOptions?, CancellationToken>((_, _, ct) => capturedToken = ct)
            .ReturnsAsync(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        var judge = new ExtensionsAIJudge(chatClient.Object);

        await judge.CompleteAsync("system", "user", cts.Token);

        Assert.Equal(cts.Token, capturedToken);
    }

    [Fact]
    public async Task CompleteAsync_ExceptionFromChatClient_Propagates()
    {
        var chatClient = new Mock<IChatClient>();
        chatClient
            .Setup(c => c.GetResponseAsync(It.IsAny<IEnumerable<ChatMessage>>(), It.IsAny<ChatOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));

        var judge = new ExtensionsAIJudge(chatClient.Object);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => judge.CompleteAsync("system", "user"));

        Assert.Equal("boom", exception.Message);
    }
}
