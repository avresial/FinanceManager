using FinanceManager.Application.Services.Ai;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Infrastructure.Services.Ai;

internal sealed class FallbackChatClient(
    IEnumerable<INamedChatClient> namedClients,
    IAiConfigurationService configService,
    ILogger<FallbackChatClient> logger) : IChatClient
{
    private sealed record ResolvedAttempt(string ProviderName, string ModelId, INamedChatClient Client);

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        CancellationToken cancellationToken = default)
    {
        List<Exception>? exceptions = null;
        var attempts = await ResolveAttemptsAsync(chatOptions, cancellationToken);
        foreach (var attempt in attempts)
        {
            var effectiveOptions = chatOptions ?? new ChatOptions();
            effectiveOptions.ModelId = attempt.ModelId;
            try
            {
                var response = await attempt.Client.GetResponseAsync(messages, effectiveOptions, cancellationToken);
                if (!string.IsNullOrWhiteSpace(response.Text))
                    return response;

                logger.LogWarning(
                    "Chat provider {Provider} with model {Model} returned empty response. Trying fallback.",
                    attempt.ProviderName,
                    attempt.ModelId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                exceptions ??= [];
                exceptions.Add(ex);
                logger.LogWarning(
                    ex,
                    "Chat provider {Provider} with model {Model} failed. Trying fallback.",
                    attempt.ProviderName,
                    attempt.ModelId);
            }
        }

        if (exceptions is not null && exceptions.Count > 0)
            throw new AggregateException("All chat providers failed.", exceptions);

        return new ChatResponse(new ChatMessage(ChatRole.Assistant, string.Empty));
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? chatOptions = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        List<Exception>? exceptions = null;
        List<ChatResponseUpdate>? selectedUpdates = null;

        var attempts = await ResolveAttemptsAsync(chatOptions, cancellationToken);
        foreach (var entry in attempts)
        {
            var effectiveOptions = chatOptions ?? new ChatOptions();
            var previousModelId = effectiveOptions.ModelId;
            effectiveOptions.ModelId = entry.ModelId;
            List<ChatResponseUpdate> bufferedUpdates = [];
            try
            {
                await foreach (var update in entry.Client.GetStreamingResponseAsync(messages, effectiveOptions, cancellationToken).WithCancellation(cancellationToken))
                    bufferedUpdates.Add(update);

                if (bufferedUpdates.Count > 0)
                {
                    selectedUpdates = bufferedUpdates;
                    break;
                }

                logger.LogWarning(
                    "Streaming chat provider {Provider} with model {Model} yielded no updates. Trying fallback.",
                    entry.ProviderName,
                    entry.ModelId);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                exceptions ??= [];
                exceptions.Add(ex);
                logger.LogWarning(
                    ex,
                    "Streaming chat provider {Provider} with model {Model} failed. Trying fallback.",
                    entry.ProviderName,
                    entry.ModelId);
            }
            finally
            {
                effectiveOptions.ModelId = previousModelId;
            }
        }

        if (selectedUpdates is not null)
        {
            foreach (var update in selectedUpdates)
                yield return update;

            yield break;
        }

        if (exceptions is not null && exceptions.Count > 0)
            throw new AggregateException("All streaming chat providers failed.", exceptions);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
        foreach (var entry in namedClients)
            entry.Dispose();
    }

    private async Task<IReadOnlyList<ResolvedAttempt>> ResolveAttemptsAsync(
        ChatOptions? chatOptions,
        CancellationToken ct)
    {
        var allClients = namedClients.ToList();
        if (allClients.Count == 0)
            return [];

        var fallbackEntries = await configService.GetFallbackEntriesAsync(ct);
        if (fallbackEntries.Count == 0)
            return [];

        var attempts = new List<ResolvedAttempt>();
        foreach (var entry in fallbackEntries.OrderBy(e => e.Order))
        {
            var provider = entry.ProviderName.Trim();
            var model = entry.Model.Trim();
            if (string.IsNullOrWhiteSpace(provider))
                continue;

            var chatClient = allClients.FirstOrDefault(x =>
                (x.ProviderName ?? string.Empty).Trim().Equals(provider, StringComparison.OrdinalIgnoreCase));

            if (chatClient is null)
                continue;

            attempts.Add(new ResolvedAttempt(provider, model, chatClient));
        }

        return attempts;
    }
}
