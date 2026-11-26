using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using System.Runtime.CompilerServices;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Shared.Repositories;

/// <summary>
/// Caches the cheap, high-frequency point-reads of an <see cref="IAccountEntryRepository{T}"/>
/// (<see cref="GetYoungest"/>, <see cref="GetOldest"/>, <see cref="GetCount"/>,
/// and <see cref="GetPostingDates"/>), keyed per account and tagged per owning user
/// (<c>global:u{userId}</c>).
/// Also caches range reads (<see cref="Get(int,DateTime,DateTime)"/>, <see cref="GetRange"/>) using
/// calendar-month buckets, inflated to whole-month boundaries on every miss, within a rolling
/// 12-month horizon. Date-boundary and relative page reads pass through with exact timestamps.
/// Any write busts the owner's cache through <see cref="ICacheInvalidator"/>,
/// including the derived dashboard tag (<c>dash:u{userId}</c>). See issues #455 and #456.
/// </summary>
public class CachedAccountEntryRepository<T>(
    IAccountEntryRepository<T> inner,
    IAccountUserResolver userResolver,
    ICacheInvalidator cacheInvalidator,
    HybridCache cache,
    EntryRangeCacheOptions rangeOptions) : IAccountEntryRepository<T> where T : class, ILedgerEntry
{
    private static readonly HybridCacheEntryOptions _pointReadOptions = new() { Expiration = TimeSpan.FromMinutes(5) };

    // ----- Cached point reads (per account, tagged per owning user) -----

    public Task<T?> GetYoungest(int accountId) => GetCached(accountId, "youngest", () => inner.GetYoungest(accountId));
    public Task<T?> GetOldest(int accountId) => GetCached(accountId, "oldest", () => inner.GetOldest(accountId));
    public Task<int> GetCount(int accountId) => GetCached(accountId, "count", () => inner.GetCount(accountId));
    public Task<List<DateTime>> GetPostingDates(int accountId) => GetCached(accountId, "dates", () => inner.GetPostingDates(accountId));

    // ----- Cached range reads: calendar-month buckets with inflate-on-miss -----

    public IAsyncEnumerable<T> Get(int accountId, DateTime startDate, DateTime endDate) =>
        Get(accountId, startDate, endDate, CancellationToken.None);

    public IAsyncEnumerable<T> Get(
        int accountId,
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
        => GetRangeInternal(accountId, startDate, endDate, cancellationToken);

    private async IAsyncEnumerable<T> GetRangeInternal(
        int accountId,
        DateTime startDate,
        DateTime endDate,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (await userResolver.GetUserId(accountId, cancellationToken) is not int userId)
        {
            await foreach (var e in inner.Get(accountId, startDate, endDate, cancellationToken))
                yield return e;
            yield break;
        }

        var now = DateTime.UtcNow;
        var horizonStart = MonthBucket.HorizonStart(rangeOptions.HorizonMonths, now);

        // Recent portion (within the rolling horizon): served from month buckets, newest-first.
        // Yield these FIRST so the overall stream remains newest-first (recent > older).
        var recentStart = startDate >= horizonStart ? startDate : horizonStart;
        if (recentStart <= endDate)
        {
            foreach (var e in await GetBucketedEntries(userId, accountId, recentStart, endDate, now, cancellationToken))
                yield return e;
        }

        // Older portion (before the horizon): direct pass-through, uncached, newest-first within
        // this sub-range. All older entries are sorted below the recent ones already yielded.
        if (startDate < horizonStart)
        {
            var olderEnd = endDate < horizonStart ? endDate : horizonStart.AddTicks(-1);
            await foreach (var e in inner.Get(accountId, startDate, olderEnd, cancellationToken))
                yield return e;
        }
    }

    public async Task<(List<T> Entries, DateTime EffectiveStartDate)> GetEntriesWithMinimumCount(
        int accountId, DateTime startDate, DateTime endDate, int minimumEntryCount = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(minimumEntryCount);

        var rangeEntries = await Get(accountId, startDate, endDate).ToListAsync();
        if (minimumEntryCount == 0 || rangeEntries.Count >= minimumEntryCount)
            return (rangeEntries, startDate);

        // Reuses the cached GetPostingDates from the point-read cache (#455) to resolve the
        // effective window without a round-trip, then fetches entries via the range bucket cache.
        // Sort newest-first so [^1] is the oldest and candidateDates[n-1] is the Nth newest.
        var candidateDates = (await GetPostingDates(accountId))
            .Where(date => date <= endDate)
            .OrderByDescending(date => date)
            .ToList();

        if (candidateDates.Count <= minimumEntryCount)
        {
            var oldestDate = candidateDates.Count != 0 && candidateDates[^1] < startDate
                ? candidateDates[^1] : startDate;
            var expandedEntries = oldestDate == startDate
                ? rangeEntries
                : await Get(accountId, oldestDate, endDate).ToListAsync();
            return (expandedEntries, oldestDate);
        }

        var nthNewestDate = candidateDates[minimumEntryCount - 1];
        var effectiveStartDate = nthNewestDate < startDate ? nthNewestDate : startDate;
        return (await Get(accountId, effectiveStartDate, endDate).ToListAsync(), effectiveStartDate);
    }

    public async Task<List<T>> GetRange(IReadOnlyCollection<int> accountIds, DateTime startDate, DateTime endDate)
    {
        if (accountIds.Count == 0) return [];

        var now = DateTime.UtcNow;
        var horizonStart = MonthBucket.HorizonStart(rangeOptions.HorizonMonths, now);
        var results = new List<T>();

        // Older portion: single uncached batch query covering all accounts.
        if (startDate < horizonStart)
        {
            var olderEnd = endDate < horizonStart ? endDate : horizonStart.AddTicks(-1);
            results.AddRange(await inner.GetRange(accountIds, startDate, olderEnd));
        }

        // Recent portion: per-(accountId, month) bucket reads; each account's buckets are shared
        // with the single-account Get path so chart-page views warm the dashboard and vice-versa.
        var recentStart = startDate >= horizonStart ? startDate : horizonStart;
        if (recentStart <= endDate)
        {
            foreach (var accountId in accountIds)
            {
                if (await userResolver.GetUserId(accountId) is not int userId)
                {
                    results.AddRange(await inner.GetRange([accountId], recentStart, endDate));
                    continue;
                }
                results.AddRange(await GetBucketedEntries(userId, accountId, recentStart, endDate, now));
            }
        }

        results.Sort((a, b) =>
        {
            var d = b.PostingDate.CompareTo(a.PostingDate);
            return d != 0 ? d : b.EntryId.CompareTo(a.EntryId);
        });

        return results;
    }

    public async Task<List<T>> GetValueRange(IReadOnlyCollection<int> accountIds, DateTime startDate, DateTime endDate)
    {
        if (accountIds.Count == 0) return [];

        var orderedIds = accountIds.Order().ToList();
        if (await userResolver.GetUserId(orderedIds[0]) is not int userId)
            return await inner.GetValueRange(orderedIds, startDate, endDate);

        var bucketStart = CalendarMonth.Of(startDate).Start(startDate.Kind);
        var bucketEnd = CalendarMonth.Of(endDate).EndInclusive();
        var cacheKey = $"{Tag(userId)}:{typeof(T).Name}:values:{string.Join('-', orderedIds)}:{bucketStart:yyyyMM}:{bucketEnd:yyyyMM}";
        var options = new HybridCacheEntryOptions
        {
            Expiration = bucketEnd >= DateTime.UtcNow
                ? rangeOptions.OpenMonthTtl
                : rangeOptions.ElapsedMonthTtl
        };

        var entries = await cache.GetOrCreateAsync(
            cacheKey,
            _ => new ValueTask<List<T>>(inner.GetValueRange(orderedIds, bucketStart, bucketEnd)),
            options,
            tags: [Tag(userId)]);

        return entries!
            .Where(entry => entry.PostingDate >= startDate && entry.PostingDate <= endDate)
            .ToList();
    }

    // ----- Pass-through reads (single-entry, relative page and batch queries) -----

    public Task<List<T>> Get(int accountId, DateTime date, int count, bool olderThenDate = true) =>
        inner.Get(accountId, date, count, olderThenDate);
    public Task<T?> Get(int accountId, long entryId) => inner.Get(accountId, entryId);
    public Task<T?> GetByOwner(int userId, long entryId, CancellationToken cancellationToken = default) => inner.GetByOwner(userId, entryId, cancellationToken);
    public IAsyncEnumerable<T> GetByOwner(int userId, int partitionId, DateTime startDate, DateTime endDate, CancellationToken cancellationToken = default) =>
        inner.GetByOwner(userId, partitionId, startDate, endDate, cancellationToken);
    public Task<IReadOnlyList<T>> GetByIds(int userId, IReadOnlyCollection<long> entryIds, CancellationToken cancellationToken = default) => inner.GetByIds(userId, entryIds, cancellationToken);

    public Task<IReadOnlyList<T>> GetRecentUnlabelled(int count, CancellationToken cancellationToken = default) => inner.GetRecentUnlabelled(count, cancellationToken);
    public Task<T?> GetNextYounger(int accountId, long entryId) => inner.GetNextYounger(accountId, entryId);
    public Task<T?> GetNextYounger(int accountId, DateTime date) =>
        inner.GetNextYounger(accountId, date);
    public Task<T?> GetNextOlder(int accountId, long entryId) => inner.GetNextOlder(accountId, entryId);
    public Task<T?> GetNextOlder(int accountId, DateTime date) =>
        inner.GetNextOlder(accountId, date);
    public Task<Dictionary<int, T>> GetNextOlder(IReadOnlyCollection<int> accountIds, DateTime date) => inner.GetNextOlder(accountIds, date);
    public Task<Dictionary<int, T>> GetNextYounger(IReadOnlyCollection<int> accountIds, DateTime date) => inner.GetNextYounger(accountIds, date);
    public Task<IReadOnlyDictionary<int, int>> GetEntriesCountPerUser(IReadOnlyCollection<int> userIds, CancellationToken cancellationToken = default) => inner.GetEntriesCountPerUser(userIds, cancellationToken);
    public Task RecalculateValues(int accountId, long entryId) => RecalculateValues(accountId, entryId, CancellationToken.None);

    public async Task RecalculateValues(int accountId, long entryId, CancellationToken cancellationToken)
    {
        try
        {
            await inner.RecalculateValues(accountId, entryId, cancellationToken);
        }
        finally
        {
            await InvalidateAccounts([accountId], cancellationToken);
        }
    }

    public Task RecalculateValues(int accountId) => RecalculateValues(accountId, CancellationToken.None);

    public async Task RecalculateValues(int accountId, CancellationToken cancellationToken)
    {
        try
        {
            await inner.RecalculateValues(accountId, cancellationToken);
        }
        finally
        {
            await InvalidateAccounts([accountId], cancellationToken);
        }
    }

    // ----- Mutating methods: invalidate the owning user's cache at the write boundary -----

    public Task<bool> Add(T entry, bool recalculate = true) => Add(entry, recalculate, CancellationToken.None);

    public async Task<bool> Add(T entry, bool recalculate, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.Add(entry, recalculate, cancellationToken);
        }
        finally
        {
            await InvalidateEntryOwners([entry]);
        }
    }

    public Task<bool> Add(IEnumerable<T> entries, bool recalculate = true) =>
        Add(entries, recalculate, CancellationToken.None);

    public async Task<bool> Add(
        IEnumerable<T> entries,
        bool recalculate,
        CancellationToken cancellationToken)
    {
        var entryList = entries as IList<T> ?? entries.ToList();
        try
        {
            return await inner.Add(entryList, recalculate, cancellationToken);
        }
        finally
        {
            await InvalidateEntryOwners(entryList);
        }
    }

    public async Task<bool> Update(T entry)
    {
        var result = await inner.Update(entry);
        await InvalidateAccounts([entry.AccountId]);
        return result;
    }

    public async Task<bool> UpdateByOwner(int userId, T entry, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.UpdateByOwner(userId, entry, cancellationToken);
        }
        finally
        {
            await InvalidateOwner(userId);
        }
    }

    public async Task<bool> DeleteByOwner(int userId, long entryId, CancellationToken cancellationToken = default)
    {
        try
        {
            return await inner.DeleteByOwner(userId, entryId, cancellationToken);
        }
        finally
        {
            await InvalidateOwner(userId);
        }
    }

    public Task<bool> Delete(int accountId, long entryId) => Delete(accountId, entryId, CancellationToken.None);

    public async Task<bool> Delete(int accountId, long entryId, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.Delete(accountId, entryId, cancellationToken);
        }
        finally
        {
            await InvalidateAccounts([accountId], CancellationToken.None);
        }
    }

    public Task<bool> Delete(int accountId) => Delete(accountId, CancellationToken.None);

    public async Task<bool> Delete(int accountId, CancellationToken cancellationToken)
    {
        try
        {
            return await inner.Delete(accountId, cancellationToken);
        }
        finally
        {
            await InvalidateAccounts([accountId], CancellationToken.None);
        }
    }

    public async Task<int> AddLabels(int userId, IEnumerable<(long entryId, int labelId)> labelAssignments, CancellationToken cancellationToken = default)
    {
        var assignments = labelAssignments as IList<(long entryId, int labelId)> ?? labelAssignments.ToList();
        var result = await inner.AddLabels(userId, assignments, cancellationToken);
        // The owner's cache tags cover linked and unlinked entries alike, and the invalidation is decoupled from the caller's token:
        // once the write has committed the cache must be invalidated even if the request is being cancelled.
        if (result > 0) await InvalidateOwner(userId);
        return result;
    }

    // ----- Private helpers -----

    // Fetches all entries for [start, end] from per-month buckets, inflating each miss to the full
    // calendar month so a partial request seeds the whole bucket. Returns entries newest-first.
    private async Task<List<T>> GetBucketedEntries(
        int userId, int accountId, DateTime start, DateTime end, DateTime now,
        CancellationToken cancellationToken = default)
    {
        var allEntries = new List<T>();

        foreach (var monthStart in MonthBucket.TouchedMonths(start, end))
        {
            var isOpenMonth = monthStart.Year == now.Year && monthStart.Month == now.Month;
            var bucketOptions = new HybridCacheEntryOptions
            {
                Expiration = isOpenMonth ? rangeOptions.OpenMonthTtl : rangeOptions.ElapsedMonthTtl
            };
            var bucketKey = MonthBucket.BucketKey(userId, accountId, monthStart);
            var monthEnd = MonthBucket.MonthEndInclusive(monthStart);

            // Capture loop variable for the factory closure.
            var ms = monthStart;
            var me = monthEnd;

            // Inflate-on-miss: the factory fetches the full calendar month (not the requested slice),
            // seeding the whole bucket so any subsequent range touching this month is a cache hit.
            var bucket = await cache.GetOrCreateAsync(
                bucketKey,
                async ct =>
                {
                    var list = new List<T>();
                    await foreach (var e in inner.Get(accountId, ms, me, ct))
                        list.Add(e);
                    return list;
                },
                bucketOptions,
                tags: [Tag(userId)],
                cancellationToken: cancellationToken);

            // Filter to the actually-requested sub-range (boundary months may be partial).
            foreach (var entry in bucket!)
            {
                if (entry.PostingDate >= start && entry.PostingDate <= end)
                    allEntries.Add(entry);
            }
        }

        // Buckets are iterated oldest-month-first; sort the combined result newest-first to match
        // the repo's ordering contract.
        allEntries.Sort((a, b) =>
        {
            var d = b.PostingDate.CompareTo(a.PostingDate);
            return d != 0 ? d : b.EntryId.CompareTo(a.EntryId);
        });

        return allEntries;
    }

    private static string Key(int userId, int accountId, string read) => $"global:u{userId}:a{accountId}:{read}";
    private static string Tag(int userId) => $"global:u{userId}";
    private static string AccountTag(int accountId) => $"global:a{accountId}";

    protected async Task<TResult> GetCached<TResult>(
        int accountId,
        string readKey,
        Func<Task<TResult>> read)
    {
        if (await userResolver.GetUserId(accountId) is not int userId)
            return await read();

        return await cache.GetOrCreateAsync(
            Key(userId, accountId, readKey),
            _ => new ValueTask<TResult>(read()),
            _pointReadOptions,
            tags: [Tag(userId)]);
    }

    // Guest owners have negative ids, so only zero is rejected. A committed write must bust the cache even when
    // the request is being cancelled, hence no caller token.
    private async Task InvalidateOwner(int userId)
    {
        if (userId != 0)
            await cacheInvalidator.InvalidateUser(userId, CancellationToken.None);
    }

    // Invalidates the stamped owner of each entry directly (an unlinked entry has no account to resolve it from)
    // and falls back to the account's owner for linked entries that carry no stamp on the caller's instance.
    private async Task InvalidateEntryOwners(IEnumerable<T> entries, CancellationToken cancellationToken = default)
    {
        var entryList = entries as IList<T> ?? entries.ToList();
        var invalidatedUsers = new HashSet<int>();
        foreach (var ownerId in entryList.Select(e => e.UserId).OfType<int>().Where(id => id != 0).Distinct())
        {
            await cacheInvalidator.InvalidateUser(ownerId, cancellationToken);
            invalidatedUsers.Add(ownerId);
        }

        await InvalidateAccounts(entryList.Where(e => e.UserId is null or 0).Select(e => e.AccountId), cancellationToken, invalidatedUsers);
    }

    private async Task InvalidateAccounts(IEnumerable<int?> accountIds, CancellationToken cancellationToken = default, ISet<int>? invalidatedUsers = null)
    {
        invalidatedUsers ??= new HashSet<int>();
        foreach (var accountId in accountIds.OfType<int>().Distinct())
        {
            await cache.RemoveByTagAsync(AccountTag(accountId), cancellationToken);

            if (await userResolver.GetUserId(accountId, cancellationToken) is int userId && invalidatedUsers.Add(userId))
                await cacheInvalidator.InvalidateUser(userId, cancellationToken);
        }
    }
}