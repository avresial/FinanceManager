using FinanceManager.Domain.Dashboard.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.FinancialAccounts.Stock.Repositories;
using Microsoft.Extensions.Caching.Hybrid;

namespace FinanceManager.Infrastructure.Repositories.Account.Entry;

/// <summary>
/// Stock flavour of <see cref="CachedAccountEntryRepository{T}"/>: caches the shared point-reads and adds
/// the instrument-aware range/boundary methods of <see cref="IStockAccountEntryRepository{T}"/> as
/// pass-throughs (range caching is out of scope for issue #455).
/// </summary>
public class CachedStockEntryRepository(
    IStockAccountEntryRepository<StockAccountEntry> inner,
    IAccountUserResolver userResolver,
    ICacheInvalidator cacheInvalidator,
    HybridCache cache,
    EntryRangeCacheOptions rangeOptions)
    : CachedAccountEntryRepository<StockAccountEntry>(inner, userResolver, cacheInvalidator, cache, rangeOptions),
      IStockAccountEntryRepository<StockAccountEntry>
{
    Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextOlder(int accountId, DateTime date) => inner.GetNextOlder(accountId, date);
    Task<Dictionary<string, StockAccountEntry>> IStockAccountEntryRepository<StockAccountEntry>.GetNextYounger(int accountId, DateTime date) => inner.GetNextYounger(accountId, date);
    public Task<Dictionary<int, Dictionary<string, StockAccountEntry>>> GetNextOlderPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date) => inner.GetNextOlderPerInstrument(accountIds, date);
    public Task<Dictionary<int, Dictionary<string, StockAccountEntry>>> GetNextYoungerPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date) => inner.GetNextYoungerPerInstrument(accountIds, date);
}