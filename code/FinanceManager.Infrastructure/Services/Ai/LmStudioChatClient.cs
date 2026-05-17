using FinanceManager.Application.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenAI;
using System.ClientModel;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class LmStudioChatClient(
    IOptions<LmStudioOptions> options,
    ILogger<LmStudioChatClient> logger) : INamedChatClient
{
    public string ProviderName => "LmStudio";

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        CancellationToken cancellationToken = default)
    {
        var modelId = chatOptions?.ModelId?.Trim();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            logger.LogWarning("LM Studio request skipped because ChatOptions.ModelId is empty.");
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty));
        }

        var openAiClient = CreateOpenAiClient();
        var chatClient = openAiClient.GetChatClient(modelId).AsIChatClient();
        return await chatClient.GetResponseAsync(messages, SanitizeOptions(chatOptions), cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        CancellationToken cancellationToken = default)
    {
        var modelId = chatOptions?.ModelId?.Trim();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            logger.LogWarning("LM Studio streaming request skipped because ChatOptions.ModelId is empty.");
            return AsyncEnumerable.Empty<ChatResponseUpdate>();
        }

        var openAiClient = CreateOpenAiClient();
        var chatClient = openAiClient.GetChatClient(modelId).AsIChatClient();
        return chatClient.GetStreamingResponseAsync(messages, SanitizeOptions(chatOptions), cancellationToken);
    }

    // Default token budget for LM Studio reasoning models (e.g. qwen3 thinking variants), which
    // spend tokens on hidden reasoning_content the OpenAI SDK does not surface. Without enough
    // headroom the visible content stays empty and the request finishes with reason "length".
    private const int _defaultMaxOutputTokens = 8192;

    // Adjusts ChatOptions for LM Studio's quirks:
    //   * response_format: {"type":"json_object"} → HTTP 400. LM Studio only accepts
    //     "json_schema" or "text", so the plain Json format is downgraded to Text. Downstream
    //     parsers already bracket-scan for JSON.
    //   * If MaxOutputTokens is not set, default it so reasoning models have room to emit
    //     visible content after their internal thinking.
    // Cloning avoids mutating shared options used by the fallback chain.
    private static ChatOptions? SanitizeOptions(ChatOptions? chatOptions)
    {
        var needsJsonDowngrade = chatOptions?.ResponseFormat is ChatResponseFormatJson { Schema: null };
        var needsTokenDefault = chatOptions?.MaxOutputTokens is null;

        if (!needsJsonDowngrade && !needsTokenDefault)
            return chatOptions;

        var clone = (chatOptions ?? new ChatOptions()).Clone();
        if (needsJsonDowngrade)
            clone.ResponseFormat = ChatResponseFormat.Text;
        if (needsTokenDefault)
            clone.MaxOutputTokens = _defaultMaxOutputTokens;
        return clone;
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