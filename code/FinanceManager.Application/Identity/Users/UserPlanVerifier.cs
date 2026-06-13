using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Stocks;
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
        // Used capacity is every record the user owns, across all account types — currency, stock and bond.
        int currencyEntries = await currencyAccountEntryRepository.GetCountForUser(userId);
        int stockEntries = await stockAccountEntryRepository.GetCountForUser(userId);
        int bondEntries = await bondAccountEntryRepository.GetCountForUser(userId);

        return currencyEntries + stockEntries + bondEntries;
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