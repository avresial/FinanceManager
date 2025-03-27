using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Infrastructure.Repositories.Account
{
    internal class InMemoryBankAccountRepository : IAccountRepository<BankAccount>
    {
        private List<BankAccount> _bankAccounts = new List<BankAccount>();

        public int? Add(int userId, string accountName)
        {
            int bankId = _bankAccounts.Count + 1;
            _bankAccounts.Add(new BankAccount(userId, bankId, accountName));
            return bankId;
        }

        public bool Delete(int accountId)
        {
            if (!_bankAccounts.Any(x => x.AccountId == accountId))
                return false;

            _bankAccounts.RemoveAll(x => x.AccountId == accountId);

            return true;
        }

        public IEnumerable<AvailableAccount> GetAvailableAccounts(int userId) => _bankAccounts.Where(x => x.UserId == userId).Select(x => new AvailableAccount(x.AccountId, x.Name));

        public BankAccount? Get(int accountId) => _bankAccounts.FirstOrDefault(x => x.AccountId == accountId);

        public bool Update(int accountId, string accountName)
        {
            var bankAccount = _bankAccounts.FirstOrDefault(x => x.AccountId == accountId);
            if (bankAccount == null) return false;

            bankAccount.Name = accountName;

            return true;
        }
    }
}
