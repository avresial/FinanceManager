using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Identity.Users;

public class UserPlanVerifier(ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> currencyAccountEntryRepository,
    IStockAccountEntryRepository<StockAccountEntry> stockAccountEntryRepository,
    IBondAccountEntryRepository<BondAccountEntry> bondAccountEntryRepository,
    IUserRepository userRepository) : IUserPlanVerifier
{
    public async Task<int> GetUsedRecordsCapacity(int userId)
    {
        var counts = await GetUsedRecordsCapacity([userId]);
        return counts.TryGetValue(userId, out var count) ? count : 0;
    }

    public async Task<IReadOnlyDictionary<int, int>> GetUsedRecordsCapacity(IReadOnlyCollection<int> userIds)
    {
        if (userIds.Count == 0) return new Dictionary<int, int>();

        // Used capacity is every record the user owns, summed across all account types — currency, stock and bond.
        var currencyCounts = await currencyAccountEntryRepository.GetEntriesCountPerUser(userIds);
        var stockCounts = await stockAccountEntryRepository.GetEntriesCountPerUser(userIds);
        var bondCounts = await bondAccountEntryRepository.GetEntriesCountPerUser(userIds);

        return userIds.Distinct().ToDictionary(
            id => id,
            id => CountFor(currencyCounts, id) + CountFor(stockCounts, id) + CountFor(bondCounts, id));

        static int CountFor(IReadOnlyDictionary<int, int> counts, int id) => counts.TryGetValue(id, out var count) ? count : 0;
    }

    public async Task<bool> CanAddMoreEntries(int userId, int entriesCount = 1)
    {
        var user = await userRepository.GetUser(userId);
        if (user is null) return false;

        int totalEntries = await GetUsedRecordsCapacity(userId);

        return totalEntries + entriesCount <= PricingProvider.GetMaxAllowedEntries(user.PricingLevel);
    }

    public async Task<bool> CanAddMoreAccounts(int userId)
    {
        var user = await userRepository.GetUser(userId);
        if (user is null) return false;

        var accountsCount = await currencyAccountRepository.GetAvailableAccounts(userId)
            .CountAsync();

        return accountsCount < PricingProvider.GetMaxAccountCount(user.PricingLevel);
    }
}