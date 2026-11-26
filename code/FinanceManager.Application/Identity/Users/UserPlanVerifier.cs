using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using FinanceManager.Domain.Transactions.Cash.Entities;
using FinanceManager.Domain.Transactions.Investments.Repositories;
using FinanceManager.Domain.Transactions.Shared.Repositories;

namespace FinanceManager.Application.Identity.Users;

public class UserPlanVerifier(ITransactionEntryRepository<CashEntry> currencyAccountEntryRepository,
    IInvestmentTransactionRepository investmentTransactionRepository,
    ITransactionEntryRepository<BondEntry> bondAccountEntryRepository,
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

        // Used capacity is every record the user owns, summed across all account types — currency,
        // investment (transactions) and bond.
        var currencyCounts = await currencyAccountEntryRepository.GetEntriesCountPerUser(userIds);
        var bondCounts = await bondAccountEntryRepository.GetEntriesCountPerUser(userIds);

        var investmentCounts = await investmentTransactionRepository.GetCountPerUser(userIds);

        return userIds.Distinct().ToDictionary(
            id => id,
            id => CountFor(currencyCounts, id) + CountFor(investmentCounts, id) + CountFor(bondCounts, id));

        static int CountFor(IReadOnlyDictionary<int, int> counts, int id) => counts.TryGetValue(id, out var count) ? count : 0;
    }

    public async Task<bool> CanAddMoreEntries(int userId, int entriesCount = 1)
    {
        var user = await userRepository.GetUser(userId);
        if (user is null) return false;

        int totalEntries = await GetUsedRecordsCapacity(userId);

        return totalEntries + entriesCount <= PricingProvider.GetMaxAllowedEntries(user.PricingLevel);
    }
}