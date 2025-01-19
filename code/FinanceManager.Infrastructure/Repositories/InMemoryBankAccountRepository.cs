using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories
{
    internal class InMemoryBankAccountRepository : IBankAccountRepository
    {
        private List<BankAccount> _bankAccounts = new List<BankAccount>();

        public bool Add(int userId, string accountName)
        {
            _bankAccounts.Add(new BankAccount(userId, _bankAccounts.Count + 1, accountName));
            return true;
        }

        public bool Delete(int accountId)
        {
            if (!_bankAccounts.Any(x => x.Id == accountId))
                return false;

            _bankAccounts.RemoveAll(x => x.Id == accountId);

            return true;
        }
        public BankAccount? Get(int accountId)
        {
            return _bankAccounts.FirstOrDefault(x => x.Id == accountId);
        }

        public bool Update(int accountId, string accountName)
        {
            var bankAccount = _bankAccounts.FirstOrDefault(x => x.Id == accountId);
            if (bankAccount == null) return false;

            bankAccount.Name = accountName;

            return true;
        }
    }
}
