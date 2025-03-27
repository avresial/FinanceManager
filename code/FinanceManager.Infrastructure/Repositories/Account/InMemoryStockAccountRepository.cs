using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Infrastructure.Repositories.Account
{
    internal class InMemoryStockAccountRepository : IAccountRepository<StockAccount>
    {
        private List<StockAccount> _bankAccounts = new List<StockAccount>();

        public int? Add(int userId, string accountName)
        {
            var accountId = _bankAccounts.Count + 1;
            _bankAccounts.Add(new StockAccount(userId, accountId, accountName));
            return accountId;
        }

        public bool Delete(int accountId)
        {
            if (!_bankAccounts.Any(x => x.AccountId == accountId))
                return false;

            _bankAccounts.RemoveAll(x => x.AccountId == accountId);

            return true;
        }

        public IEnumerable<AvailableAccount> GetAvailableAccounts(int accountId) =>
            _bankAccounts.Where(x => x.UserId == accountId).Select(x => new AvailableAccount(x.AccountId, x.Name));

        public StockAccount? Get(int accountId) => _bankAccounts.FirstOrDefault(x => x.AccountId == accountId);

        public bool Update(int accountId, string accountName)
        {
            var bankAccount = _bankAccounts.FirstOrDefault(x => x.AccountId == accountId);
            if (bankAccount == null) return false;

            bankAccount.Name = accountName;

            return true;
        }
    }
}
