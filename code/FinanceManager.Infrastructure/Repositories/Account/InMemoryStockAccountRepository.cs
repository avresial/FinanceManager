using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Infrastructure.Repositories.Account
{
    internal class InMemoryStockAccountRepository : IAccountRepository<StockAccount>
    {
        private List<StockAccount> _bankAccounts = new List<StockAccount>();

        public int? Add(int accountId, int userId, string accountName)
        {
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

        public IEnumerable<AvailableAccount> GetAvailableAccounts(int userId) =>
            _bankAccounts.Where(x => x.UserId == userId).Select(x => new AvailableAccount(x.AccountId, x.Name));

        public StockAccount? Get(int accountId)
        {
            var accountToReturn = _bankAccounts.FirstOrDefault(x => x.AccountId == accountId);
            if (accountToReturn is null) return null;
            return new StockAccount(accountToReturn.UserId, accountToReturn.AccountId, accountToReturn.Name);
        }

        public bool Update(int accountId, string accountName)
        {
            var bankAccount = _bankAccounts.FirstOrDefault(x => x.AccountId == accountId);
            if (bankAccount == null) return false;

            bankAccount.Name = accountName;

            return true;
        }
    }
}
