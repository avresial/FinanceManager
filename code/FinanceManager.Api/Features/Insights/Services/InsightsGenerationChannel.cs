using FinanceManager.Domain.Shared.Services;
using System.Threading.Channels;

namespace FinanceManager.Api.Features.Insights.Services;

public sealed class InsightsGenerationChannel(IDateTimeProvider dateTimeProvider) : IInsightsGenerationChannel
{
    /// <summary>
    /// Shortest gap between two accepted requests for the same user. The dashboard asks on every read, and an
    /// accepted request costs a full AI generation, so requests are collapsed: long enough that revisiting a page
    /// is free and an in-flight run is never queued twice, short enough that a failed run is retried the same day.
    /// </summary>
    private static readonly TimeSpan _queueCooldown = TimeSpan.FromHours(1);

    /// <summary>Size at which expired entries are dropped, so the map tracks recent askers rather than every user ever seen.</summary>
    private const int _pruneThreshold = 1024;

    private readonly Lock _lastQueuedLock = new();
    private readonly Dictionary<int, DateTime> _lastQueued = [];

    private readonly Channel<int> _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(256)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.DropOldest
    });

    public async ValueTask<bool> QueueUser(int userId, CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;

        lock (_lastQueuedLock)
        {
            if (_lastQueued.TryGetValue(userId, out var lastQueuedAt) && now - lastQueuedAt < _queueCooldown)
                return false;

            if (_lastQueued.Count >= _pruneThreshold)
                PruneExpired(now);

            _lastQueued[userId] = now;
        }

        try
        {
            await _channel.Writer.WriteAsync(userId, cancellationToken);
            return true;
        }
        catch
        {
            // Nothing was enqueued, so the cooldown this call claimed would silence the user for an hour over a
            // request that never reached the worker — a caller passing a request-scoped token only has to
            // disconnect to trigger it. Give the slot back, unless a later call has already taken it.
            lock (_lastQueuedLock)
            {
                if (_lastQueued.TryGetValue(userId, out var claimedAt) && claimedAt == now)
                    _lastQueued.Remove(userId);
            }

            throw;
        }
    }

    public IAsyncEnumerable<int> ReadAll(CancellationToken cancellationToken) =>
        _channel.Reader.ReadAllAsync(cancellationToken);

    private void PruneExpired(DateTime now)
    {
        var expired = _lastQueued.Where(x => now - x.Value >= _queueCooldown).Select(x => x.Key).ToList();
        foreach (var userId in expired)
            _lastQueued.Remove(userId);
    }
}