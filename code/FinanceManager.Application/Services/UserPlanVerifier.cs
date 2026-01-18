using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities.FinancialAccounts.Currency;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services;

public class UserPlanVerifier(ICurrencyAccountRepository<CurrencyAccount> bankAccountRepository, IAccountEntryRepository<CurrencyAccountEntry> bankAccountEntryRepository,
    IUserRepository userRepository) : IUserPlanVerifier
{
    public async Task<int> GetUsedRecordsCapacity(int userId)
    {
        int totalEntries = 0;

        foreach (var account in await bankAccountRepository.GetAvailableAccounts(userId).ToListAsync())
            totalEntries += await bankAccountEntryRepository.GetCount(account.AccountId);

        return await Task.FromResult(totalEntries);
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

        var accountsCount = await bankAccountRepository.GetAvailableAccounts(userId)
            .CountAsync();

        return accountsCount < PricingProvider.GetMaxAccountCount(user.PricingLevel);
    }
}