using FinanceManager.Application.Options;
using GitHub.Copilot.SDK;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class CopilotChatClient(
    IOptions<GitHubModelsOptions> options,
    ILogger<CopilotChatClient> logger) : IChatClient
{
    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        CancellationToken cancellationToken = default)
    {
        var messageList = messages.ToList();
        var systemPrompt = messageList.FirstOrDefault(m => m.Role == ChatRole.System)?.Text ?? string.Empty;
        var userPrompt = messageList.LastOrDefault(m => m.Role == ChatRole.User)?.Text ?? string.Empty;

        var model = options.Value.Model;
        var timeoutSeconds = options.Value.RequestTimeoutSeconds > 0
            ? options.Value.RequestTimeoutSeconds
            : 60;

        await using var client = new CopilotClient();
        string? sessionId = null;
        try
        {
            await client.StartAsync(cancellationToken);

            var sessionConfig = new SessionConfig { Model = model };
            if (!string.IsNullOrWhiteSpace(systemPrompt))
            {
                sessionConfig.SystemMessage = new SystemMessageConfig
                {
                    Mode = SystemMessageMode.Replace,
                    Content = systemPrompt
                };
            }

            await using var session = await client.CreateSessionAsync(sessionConfig, cancellationToken);
            sessionId = session.SessionId;

            var response = await session.SendAndWaitAsync(
                new MessageOptions { Prompt = userPrompt },
                TimeSpan.FromSeconds(timeoutSeconds),
                cancellationToken);

            var content = response?.Data?.Content ?? string.Empty;
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, content));
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "GitHub Copilot SDK request timed out");
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GitHub Copilot SDK request failed");
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty));
        }
        finally
        {
            try
            {
                await client.StopAsync();
                if (sessionId is not null)
                    await client.DeleteSessionAsync(sessionId, CancellationToken.None);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "GitHub Copilot SDK shutdown failed");
            }
        }
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Streaming is not supported by the GitHub Copilot SDK provider.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
