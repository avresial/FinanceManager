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
    HybridCache cache)
    : CachedAccountEntryRepository<BondAccountEntry>(inner, userResolver, cacheInvalidator, cache),
      IBondAccountEntryRepository<BondAccountEntry>
{
    Task<Dictionary<int, BondAccountEntry>> IBondAccountEntryRepository<BondAccountEntry>.GetNextOlder(int accountId, DateTime date) => inner.GetNextOlder(accountId, date);
    Task<Dictionary<int, BondAccountEntry>> IBondAccountEntryRepository<BondAccountEntry>.GetNextYounger(int accountId, DateTime date) => inner.GetNextYounger(accountId, date);
    public Task<Dictionary<int, Dictionary<int, BondAccountEntry>>> GetNextOlderPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date) => inner.GetNextOlderPerInstrument(accountIds, date);
    public Task<Dictionary<int, Dictionary<int, BondAccountEntry>>> GetNextYoungerPerInstrument(IReadOnlyCollection<int> accountIds, DateTime date) => inner.GetNextYoungerPerInstrument(accountIds, date);
}