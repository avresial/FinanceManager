using System.Collections.Concurrent;

namespace FinanceManager.Api.Services.Guest;

public sealed class GuestSessionStore : IGuestSessionStore
{
    private readonly ConcurrentDictionary<int, DateTime> _sessions = new();
    private int _nextGuestId;

    public int CreateSession()
    {
        // Negative ids keep guest sandbox keys separate from real user ids (which are positive identity columns).
        var guestUserId = Interlocked.Decrement(ref _nextGuestId);
        _sessions[guestUserId] = DateTime.UtcNow;
        return guestUserId;
    }

    public bool IsActive(int guestUserId) => _sessions.ContainsKey(guestUserId);

    public IReadOnlyCollection<int> GetExpired(TimeSpan ttl)
    {
        var threshold = DateTime.UtcNow - ttl;
        return _sessions
            .Where(kvp => kvp.Value <= threshold)
            .Select(kvp => kvp.Key)
            .ToArray();
    }

    public bool Remove(int guestUserId) => _sessions.TryRemove(guestUserId, out _);
}
