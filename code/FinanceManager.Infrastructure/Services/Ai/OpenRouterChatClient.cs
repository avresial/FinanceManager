using FinanceManager.Application.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class OpenRouterChatClient(
    IOptions<OpenRouterOptions> options,
    ILogger<OpenRouterChatClient> logger) : INamedChatClient
{
    public string ProviderName => "OpenRouter";

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        CancellationToken cancellationToken = default)
    {
        var modelId = chatOptions?.ModelId?.Trim();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            logger.LogWarning("OpenRouter request skipped because ChatOptions.ModelId is empty.");
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty));
        }

        var openAiClient = CreateOpenAiClient();
        var chatClient = openAiClient.GetChatClient(modelId).AsIChatClient();
        return await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        CancellationToken cancellationToken = default)
    {
        var modelId = chatOptions?.ModelId?.Trim();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            logger.LogWarning("OpenRouter streaming request skipped because ChatOptions.ModelId is empty.");
            return AsyncEnumerable.Empty<ChatResponseUpdate>();
        }

        var openAiClient = CreateOpenAiClient();
        var chatClient = openAiClient.GetChatClient(modelId).AsIChatClient();
        return chatClient.GetStreamingResponseAsync(messages, chatOptions, cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    private OpenAIClient CreateOpenAiClient()
    {
        var config = options.Value;
        var timeoutSeconds = config.RequestTimeoutSeconds > 0 ? config.RequestTimeoutSeconds : 180;
        return new OpenAIClient(
            new ApiKeyCredential(config.ApiKey),
            new OpenAIClientOptions
            {
                Endpoint = new Uri(config.BaseUrl),
                NetworkTimeout = TimeSpan.FromSeconds(timeoutSeconds)
            });
    }
}
