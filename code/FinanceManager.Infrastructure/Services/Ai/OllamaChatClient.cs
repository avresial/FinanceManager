using FinanceManager.Application.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class OllamaChatClient(
    IOptions<OllamaOptions> options,
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

        using var client = new OllamaApiClient(new Uri(options.Value.BaseUrl), modelId);
        var chatClient = (IChatClient)client;
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
            logger.LogWarning("Ollama streaming request skipped because ChatOptions.ModelId is empty.");
            return AsyncEnumerable.Empty<ChatResponseUpdate>();
        }

        var client = new OllamaApiClient(new Uri(options.Value.BaseUrl), modelId);
        var chatClient = (IChatClient)client;
        return chatClient.GetStreamingResponseAsync(messages, chatOptions, cancellationToken);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose() { }
}
