using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

/// <summary>
/// Bond flavour of <see cref="CachedAccountEntryRepository{T}"/>: caches the shared point-reads and adds
/// the instrument-aware range/boundary methods of <see cref="IBondAccountEntryRepository{T}"/> as
/// pass-throughs (range caching is out of scope for issue #455).
/// </summary>
public class CachedBondEntryRepository(
    IBondAccountEntryRepository<BondAccountEntry> inner,
    IAccountUserResolver userResolver,
    ICacheInvalidator cacheInvalidator,
    HybridCache cache,
    EntryRangeCacheOptions rangeOptions)
    : CachedAccountEntryRepository<BondAccountEntry>(inner, userResolver, cacheInvalidator, cache, rangeOptions),
      IBondAccountEntryRepository<BondAccountEntry>
{
    private static readonly HybridCacheEntryOptions _boundaryOptions = new() { Expiration = TimeSpan.FromMinutes(5) };

    Task<Dictionary<int, BondAccountEntry>> IBondAccountEntryRepository<BondAccountEntry>.GetNextOlder(int accountId, DateTime date) =>
        GetBoundary(accountId, date, "older", inner.GetNextOlderPerInstrument);

    Task<Dictionary<int, BondAccountEntry>> IBondAccountEntryRepository<BondAccountEntry>.GetNextYounger(int accountId, DateTime date) =>
        GetBoundary(accountId, date, "younger", inner.GetNextYoungerPerInstrument);

    public Task<Dictionary<int, Dictionary<int, BondAccountEntry>>> GetNextOlderPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date) => inner.GetNextOlderPerInstrument(accountIds, date);
    public Task<Dictionary<int, Dictionary<int, BondAccountEntry>>> GetNextYoungerPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date) => inner.GetNextYoungerPerInstrument(accountIds, date);

    private async Task<Dictionary<int, BondAccountEntry>> GetBoundary(
        int accountId,
        DateTime date,
        string direction,
        Func<IReadOnlyCollection<int>, DateTime, Task<Dictionary<int, Dictionary<int, BondAccountEntry>>>> read)
    {
        async Task<Dictionary<int, BondAccountEntry>> Read()
        {
            var byAccount = await read([accountId], date);
            return byAccount.GetValueOrDefault(accountId) ?? [];
        }

        if (await AccountUserResolver.GetUserId(accountId) is not int userId)
            return await Read();

        return await EntryCache.GetOrCreateAsync(
            $"global:u{userId}:a{accountId}:bond:{direction}:{date.Ticks}",
            _ => new ValueTask<Dictionary<int, BondAccountEntry>>(Read()),
            _boundaryOptions,
            tags: [$"global:u{userId}"]);
    }
}