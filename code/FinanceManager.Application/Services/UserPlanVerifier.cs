using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Application.Services;
public class UserPlanVerifier(IBankAccountRepository<BankAccount> bankAccountRepository, IAccountEntryRepository<BankAccountEntry> bankAccountEntryRepository)
{
    private readonly IBankAccountRepository<BankAccount> _bankAccountRepository = bankAccountRepository;
    private readonly IAccountEntryRepository<BankAccountEntry> _bankAccountEntryRepository = bankAccountEntryRepository;

    private readonly int _maxAllowedEntries = 10;

    public bool CanAddMoreEntries(int userId)
    {
        var accounts = _bankAccountRepository.GetAvailableAccounts(userId);
        int totalEntries = 0;
        foreach (var account in accounts)
        {
            var count = _bankAccountEntryRepository.GetCount(account.AccountId);
            if (count is null) continue;
            totalEntries += count.Value;
        }

        return totalEntries < _maxAllowedEntries;
    }
}
