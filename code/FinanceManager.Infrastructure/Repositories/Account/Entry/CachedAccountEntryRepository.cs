using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

/// <summary>
/// Caches the cheap, high-frequency point-reads of an <see cref="IAccountEntryRepository{T}"/>
/// (<see cref="GetYoungest"/>, <see cref="GetOldest"/>, <see cref="GetCount"/>,
/// <see cref="GetPostingDates"/>), keyed per account and tagged per owning user (<c>acc:u{userId}</c>).
/// Every mutating method busts the owner's cache through <see cref="ICacheInvalidator"/>, which clears both
/// the entry tag and the derived dashboard tag (<c>dash:u{userId}</c>). Because a single write recalculates
/// the running balance of every entry from the changed date forward, coarse per-user invalidation is
/// required for correctness — not merely convenient. Range and relative reads are passed straight through;
/// caching those is deferred to a follow-up issue. See issue #455.
/// </summary>
public class CachedAccountEntryRepository<T>(
    IAccountEntryRepository<T> inner,
    IAccountUserResolver userResolver,
    ICacheInvalidator cacheInvalidator,
    HybridCache cache) : IAccountEntryRepository<T> where T : FinancialEntryBase
{
    private static readonly HybridCacheEntryOptions _cacheOptions = new() { Expiration = TimeSpan.FromMinutes(5) };

    // ----- Cached point reads (per account, tagged per owning user) -----

    public async Task<T?> GetYoungest(int accountId)
    {
        if (await userResolver.GetUserId(accountId) is not int userId)
            return await inner.GetYoungest(accountId);

        return await cache.GetOrCreateAsync<T?>(
            Key(userId, accountId, "youngest"),
            _ => new ValueTask<T?>(inner.GetYoungest(accountId)),
            _cacheOptions,
            tags: [Tag(userId)]);
    }

    public async Task<T?> GetOldest(int accountId)
    {
        if (await userResolver.GetUserId(accountId) is not int userId)
            return await inner.GetOldest(accountId);

        return await cache.GetOrCreateAsync<T?>(
            Key(userId, accountId, "oldest"),
            _ => new ValueTask<T?>(inner.GetOldest(accountId)),
            _cacheOptions,
            tags: [Tag(userId)]);
    }

    public async Task<int> GetCount(int accountId)
    {
        if (await userResolver.GetUserId(accountId) is not int userId)
            return await inner.GetCount(accountId);

        return await cache.GetOrCreateAsync(
            Key(userId, accountId, "count"),
            _ => new ValueTask<int>(inner.GetCount(accountId)),
            _cacheOptions,
            tags: [Tag(userId)]);
    }

    public async Task<List<DateTime>> GetPostingDates(int accountId)
    {
        if (await userResolver.GetUserId(accountId) is not int userId)
            return await inner.GetPostingDates(accountId);

        return await cache.GetOrCreateAsync(
            Key(userId, accountId, "dates"),
            _ => new ValueTask<List<DateTime>>(inner.GetPostingDates(accountId)),
            _cacheOptions,
            tags: [Tag(userId)]);
    }

    // ----- Pass-through reads (range / relative; not cached in this issue) -----

    public IAsyncEnumerable<T> Get(int accountId, DateTime startDate, DateTime endDate) => inner.Get(accountId, startDate, endDate);
    public Task<List<T>> Get(int accountId, DateTime date, int count, bool olderThenDate = true) => inner.Get(accountId, date, count, olderThenDate);
    public Task<(List<T> Entries, DateTime EffectiveStartDate)> GetEntriesWithMinimumCount(int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0) => inner.GetEntriesWithMinimumCount(accountId, startDate, endDate, minimumEntryCount);
    public Task<T?> Get(int accountId, int entryId) => inner.Get(accountId, entryId);
    public Task<IReadOnlyList<T>> GetByIds(IReadOnlyCollection<int> entryIds, CancellationToken cancellationToken = default) => inner.GetByIds(entryIds, cancellationToken);
    public Task<IReadOnlyList<T>> GetRecentUnlabelled(int count, CancellationToken cancellationToken = default) => inner.GetRecentUnlabelled(count, cancellationToken);
    public Task<T?> GetNextYounger(int accountId, int entryId) => inner.GetNextYounger(accountId, entryId);
    public Task<T?> GetNextYounger(int accountId, DateTime date) => inner.GetNextYounger(accountId, date);
    public Task<T?> GetNextOlder(int accountId, int entryId) => inner.GetNextOlder(accountId, entryId);
    public Task<T?> GetNextOlder(int accountId, DateTime date) => inner.GetNextOlder(accountId, date);
    public Task<List<T>> GetRange(IReadOnlyCollection<int> accountIds, DateTime startDate, DateTime endDate) => inner.GetRange(accountIds, startDate, endDate);
    public Task<Dictionary<int, T>> GetNextOlder(IReadOnlyCollection<int> accountIds, DateTime date) => inner.GetNextOlder(accountIds, date);
    public Task<Dictionary<int, T>> GetNextYounger(IReadOnlyCollection<int> accountIds, DateTime date) => inner.GetNextYounger(accountIds, date);
    public Task<IReadOnlyDictionary<int, int>> GetEntriesCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default) => inner.GetEntriesCountPerUser(userIds, cancellationToken);
    public Task RecalculateValues(int accountId, int entryId) => inner.RecalculateValues(accountId, entryId);
    public Task RecalculateValues(int accountId) => inner.RecalculateValues(accountId);

    // ----- Mutating methods: invalidate the owning user's cache at the write boundary -----

    public async Task<bool> Add(T entry, bool recalculate = true)
    {
        var result = await inner.Add(entry, recalculate);
        await InvalidateAccounts([entry.AccountId]);
        return result;
    }

    public async Task<bool> Add(IEnumerable<T> entries, bool recalculate = true)
    {
        var entryList = entries as IList<T> ?? entries.ToList();
        var result = await inner.Add(entryList, recalculate);
        await InvalidateAccounts(entryList.Select(e => e.AccountId));
        return result;
    }

    public async Task<bool> Update(T entry)
    {
        var result = await inner.Update(entry);
        await InvalidateAccounts([entry.AccountId]);
        return result;
    }

    public async Task<bool> Delete(int accountId, int entryId)
    {
        var result = await inner.Delete(accountId, entryId);
        await InvalidateAccounts([accountId]);
        return result;
    }

    public async Task<bool> Delete(int accountId)
    {
        var result = await inner.Delete(accountId);
        await InvalidateAccounts([accountId]);
        return result;
    }

    public async Task<bool> AddLabel(int entryId, int labelId)
    {
        var result = await inner.AddLabel(entryId, labelId);
        await InvalidateEntries([entryId]);
        return result;
    }

    public async Task<int> AddLabels(IEnumerable<(int entryId, int labelId)> labelAssignments, CancellationToken cancellationToken = default)
    {
        var assignments = labelAssignments as IList<(int entryId, int labelId)> ?? labelAssignments.ToList();
        var result = await inner.AddLabels(assignments, cancellationToken);
        // Decouple the post-write cache bust from the caller's token: once the write has committed the
        // cache must be invalidated even if the request is being cancelled, or stale entries would linger.
        await InvalidateEntries(assignments.Select(a => a.entryId), CancellationToken.None);
        return result;
    }

    // ----- Helpers -----

    private static string Key(int userId, int accountId, string read) => $"acc:u{userId}:a{accountId}:{read}";
    private static string Tag(int userId) => $"acc:u{userId}";

    private async Task InvalidateAccounts(IEnumerable<int> accountIds, CancellationToken cancellationToken = default)
    {
        var invalidatedUsers = new HashSet<int>();
        foreach (var accountId in accountIds.Distinct())
        {
            if (await userResolver.GetUserId(accountId, cancellationToken) is int userId && invalidatedUsers.Add(userId))
                await cacheInvalidator.InvalidateUser(userId, cancellationToken);
        }
    }

    private async Task InvalidateEntries(IEnumerable<int> entryIds, CancellationToken cancellationToken = default)
    {
        // Label writes only carry entry ids; resolve the owning accounts so the per-user tag can be built.
        var ids = entryIds.Distinct().ToList();
        if (ids.Count == 0) return;

        var accountIds = (await inner.GetByIds(ids, cancellationToken)).Select(e => e.AccountId);
        await InvalidateAccounts(accountIds, cancellationToken);
    }
}