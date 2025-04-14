using FinanceManager.Application.Providers;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services;

public class UserPlanVerifier(IBankAccountRepository<BankAccount> bankAccountRepository, IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository,
    IUserRepository userRepository, PricingProvider pricingProvider)
{
    private readonly IBankAccountRepository<BankAccount> _bankAccountRepository = bankAccountRepository;
    private readonly IAccountEntryRepository<BankAccountEntry> _bankAccountEntryRepository = bankAccountEntryRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly PricingProvider _pricingProvider = pricingProvider;

    public async Task<bool> CanAddMoreEntries(int userId)
    {
        var user = await _userRepository.GetUser(userId);
        if (user is null) return false;

        var accounts = _bankAccountRepository.GetAvailableAccounts(userId);
        int totalEntries = 0;
        foreach (var account in accounts)
        {
            var count = _bankAccountEntryRepository.GetCount(account.AccountId);
            if (count is null) continue;
            totalEntries += count.Value;
        }

        return totalEntries < _pricingProvider.GetMaxAllowedEntries(user.PricingLevel);
    }

    public async Task<bool> CanAddMoreAccounts(int userId)
    {
        var user = await _userRepository.GetUser(userId);
        if (user is null) return false;

        var accounts = _bankAccountRepository.GetAvailableAccounts(userId);

        return accounts.Count() < _pricingProvider.GetMaxAllowedEntries(user.PricingLevel);
    }
}
