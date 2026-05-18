using FinanceManager.Application.Services.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OllamaSharp;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class OllamaChatClient(
    IAiConfigurationService configService,
    ILogger<OllamaChatClient> logger) : INamedChatClient
{
    public string ProviderName => "Ollama";

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        CancellationToken cancellationToken = default)
    {
        var modelId = chatOptions?.ModelId?.Trim();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            logger.LogWarning("Ollama request skipped because ChatOptions.ModelId is empty.");
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty));
        }

        var config = await configService.GetProviderAsync("Ollama", cancellationToken);
        using var client = new OllamaApiClient(new Uri(config.BaseUrl), modelId);
        var chatClient = (IChatClient)client;
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
            logger.LogWarning("Ollama streaming request skipped because ChatOptions.ModelId is empty.");
            yield break;
        }

        var config = await configService.GetProviderAsync("Ollama", cancellationToken);
        var client = new OllamaApiClient(new Uri(config.BaseUrl), modelId);
        var chatClient = (IChatClient)client;
        await foreach (var update in chatClient.GetStreamingResponseAsync(messages, chatOptions, cancellationToken))
            yield return update;
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
