using FinanceManager.Application.Services.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using System.ClientModel;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class OpenRouterChatClient(
    IAiConfigurationService configService,
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

        var openAiClient = await CreateOpenAiClientAsync(cancellationToken);
        var chatClient = openAiClient.GetChatClient(modelId).AsIChatClient();
        return await chatClient.GetResponseAsync(messages, chatOptions, cancellationToken);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var modelId = chatOptions?.ModelId?.Trim();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            logger.LogWarning("OpenRouter streaming request skipped because ChatOptions.ModelId is empty.");
            yield break;
        }

        var openAiClient = await CreateOpenAiClientAsync(cancellationToken);
        var chatClient = openAiClient.GetChatClient(modelId).AsIChatClient();
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, chatOptions, cancellationToken))
            yield return update;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }

    private async Task<OpenAIClient> CreateOpenAiClientAsync(CancellationToken ct)
    {
        var config = await configService.GetProviderAsync("OpenRouter", ct);
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