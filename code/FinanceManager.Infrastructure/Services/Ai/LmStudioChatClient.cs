using FinanceManager.Application.Services.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenAI;
using System.ClientModel;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class LmStudioChatClient(
    IAiConfigurationService configService,
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

        var openAiClient = await CreateOpenAiClientAsync(cancellationToken);
        var chatClient = openAiClient.GetChatClient(modelId).AsIChatClient();
        var response = await chatClient.GetResponseAsync(messages, SanitizeOptions(chatOptions), cancellationToken);

        if (string.IsNullOrWhiteSpace(response.Text))
        {
            logger.LogWarning(
                "LM Studio returned empty visible content. FinishReason={FinishReason}, InputTokens={InputTokens}, OutputTokens={OutputTokens}, TotalTokens={TotalTokens}. This usually means the reasoning model consumed the entire MaxOutputTokens budget on hidden thinking. Raise LmStudio MaxOutputTokens or shrink the batch.",
                response.FinishReason?.ToString() ?? "(none)",
                response.Usage?.InputTokenCount,
                response.Usage?.OutputTokenCount,
                response.Usage?.TotalTokenCount);
        }

        return response;
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var modelId = chatOptions?.ModelId?.Trim();
        if (string.IsNullOrWhiteSpace(modelId))
        {
            logger.LogWarning("LM Studio streaming request skipped because ChatOptions.ModelId is empty.");
            yield break;
        }

        var openAiClient = await CreateOpenAiClientAsync(cancellationToken);
        var chatClient = openAiClient.GetChatClient(modelId).AsIChatClient();
        return chatClient.GetStreamingResponseAsync(messages, SanitizeOptions(chatOptions), cancellationToken);
    }

    // Default token budget for LM Studio reasoning models (e.g. qwen3 thinking variants), which
    // spend tokens on hidden reasoning_content the OpenAI SDK does not surface. Without enough
    // headroom the visible content stays empty and the request finishes with reason "length".
    // 16384 gives Qwen3-35B room to think on a 20-entry labeling batch and still emit the JSON.
    private const int _defaultMaxOutputTokens = 16384;

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

    private async Task<OpenAIClient> CreateOpenAiClientAsync(CancellationToken ct)
    {
        var config = await configService.GetProviderAsync("LmStudio", ct);
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