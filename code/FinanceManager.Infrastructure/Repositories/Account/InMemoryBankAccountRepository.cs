using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories.Account
{
    internal class InMemoryBankAccountRepository : IAccountRepository<BankAccount>
    {
        private List<BankAccount> _bankAccounts = new List<BankAccount>();

        public bool Add(int userId, string accountName)
        {
            _bankAccounts.Add(new BankAccount(userId, _bankAccounts.Count + 1, accountName));
            return true;
        }

        public bool Delete(int accountId)
        {
            if (!_bankAccounts.Any(x => x.AccountId == accountId))
                return false;

            _bankAccounts.RemoveAll(x => x.AccountId == accountId);

            return true;
        }

        public IList<(int, string)> GetAvailableAccounts(int userId) => _bankAccounts.Where(x => x.UserId == userId).Select(x => (x.AccountId, x.Name)).ToList();

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
