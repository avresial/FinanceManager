using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services;

public class UserPlanVerifier(IBankAccountRepository<BankAccount> bankAccountRepository, IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository,
    IUserRepository userRepository, PricingProvider pricingProvider) : IUserPlanVerifier
{
    public async Task<int> GetUsedRecordsCapacity(int userId)
    {
        int totalEntries = 0;

        await foreach (var account in bankAccountRepository.GetAvailableAccounts(userId))
            totalEntries += await bankAccountEntryRepository.GetCount(account.AccountId);

        return await Task.FromResult(totalEntries);
    }

    public async Task<bool> CanAddMoreEntries(int userId, int entriesCount = 1)
    {
        var user = await userRepository.GetUser(userId);
        if (user is null) return false;

        int totalEntries = await GetUsedRecordsCapacity(userId);

        return totalEntries + entriesCount <= pricingProvider.GetMaxAllowedEntries(user.PricingLevel);
    }

    public async Task<bool> CanAddMoreAccounts(int userId)
    {
        var user = await userRepository.GetUser(userId);
        if (user is null) return false;

        var accountsCount = await bankAccountRepository.GetAvailableAccounts(userId)
            .CountAsync();

        return accountsCount < pricingProvider.GetMaxAccountCount(user.PricingLevel);
    }
}
