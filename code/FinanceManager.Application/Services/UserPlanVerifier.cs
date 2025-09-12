using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services;

public class UserPlanVerifier(IBankAccountRepository<BankAccount> bankAccountRepository, IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository,
    IUserRepository userRepository, PricingProvider pricingProvider)
{
    public async Task<int> GetUsedRecordsCapacity(int userId)
    {
        var accounts = await bankAccountRepository.GetAvailableAccounts(userId);
        int totalEntries = 0;

        foreach (var account in accounts)
            totalEntries += await bankAccountEntryRepository.GetCount(account.AccountId);

        return await Task.FromResult(totalEntries);
    }

    public async Task<bool> CanAddMoreEntries(int userId)
    {
        var user = await userRepository.GetUser(userId);
        if (user is null) return false;

        int totalEntries = await GetUsedRecordsCapacity(userId);

        return totalEntries < pricingProvider.GetMaxAllowedEntries(user.PricingLevel);
    }

    public async Task<bool> CanAddMoreAccounts(int userId)
    {
        var user = await userRepository.GetUser(userId);
        if (user is null) return false;

        var accounts = await bankAccountRepository.GetAvailableAccounts(userId);

        return accounts.Count() < pricingProvider.GetMaxAccountCount(user.PricingLevel);
    }
}
