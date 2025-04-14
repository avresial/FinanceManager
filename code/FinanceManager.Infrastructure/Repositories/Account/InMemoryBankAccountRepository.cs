using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Infrastructure.Repositories.Account;

internal class InMemoryBankAccountRepository : IBankAccountRepository<BankAccount>
{
    private List<BankAccount> _bankAccounts = [];
    public int? GetLastAccountId()
    {
        if (_bankAccounts.Count != 0)
            return _bankAccounts.Max(x => x.AccountId);
        return null;
    }
    public int? Add(int userId, int accountId, string accountName) => Add(userId, accountId, accountName, AccountType.Other);
    public int? Add(int userId, int accountId, string accountName, AccountType accountType)
    {
        _bankAccounts.Add(new BankAccount(userId, accountId, accountName, accountType));
        return accountId;
    }
    public bool Delete(int accountId)
    {
        if (!_bankAccounts.Any(x => x.AccountId == accountId))
            return false;

        _bankAccounts.RemoveAll(x => x.AccountId == accountId);

        return true;
    }
    public IEnumerable<AvailableAccount> GetAvailableAccounts(int userId) => _bankAccounts.Where(x => x.UserId == userId).Select(x => new AvailableAccount(x.AccountId, x.Name));

    public BankAccount? Get(int accountId)
    {
        var accountToReturn = _bankAccounts.FirstOrDefault(x => x.AccountId == accountId);
        if (accountToReturn is null) return null;
        return new BankAccount(accountToReturn.UserId, accountToReturn.AccountId, accountToReturn.Name, accountToReturn.AccountType);
    }

    public bool Update(int accountId, string accountName)
    {
        var bankAccount = _bankAccounts.FirstOrDefault(x => x.AccountId == accountId);
        if (bankAccount == null) return false;

        bankAccount.Name = accountName;

        return true;
    }
    public bool Update(int accountId, string accountName, AccountType accountType)
    {
        var bankAccount = _bankAccounts.FirstOrDefault(x => x.AccountId == accountId);
        if (bankAccount == null) return false;

        bankAccount.Name = accountName;
        bankAccount.AccountType = accountType;

        return true;
    }


}
