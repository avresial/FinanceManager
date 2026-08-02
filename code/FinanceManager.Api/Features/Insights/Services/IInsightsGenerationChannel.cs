namespace FinanceManager.Api.Features.Insights.Services;

public interface IInsightsGenerationChannel
{
    /// <summary>
    /// Queues a user for insights generation. Returns <c>false</c> when the request was dropped because the
    /// same user was queued moments ago — callers may ask on every read, so the channel, not the caller, owns
    /// how often a user's generation may actually run.
    /// </summary>
    ValueTask<bool> QueueUser(int userId, CancellationToken cancellationToken = default);

    IAsyncEnumerable<int> ReadAll(CancellationToken cancellationToken);
}