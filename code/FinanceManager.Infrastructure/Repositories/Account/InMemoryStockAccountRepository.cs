using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Infrastructure.Repositories.Account
{
    internal class InMemoryStockAccountRepository : IAccountRepository<StockAccount>
    {
        private List<StockAccount> accounts = [];

        public int GetAccountsCount()
        {
            return accounts.Count();
        }
        public async Task<int?> Add(int userId, int accountId, string accountName)
        {
            accounts.Add(new StockAccount(userId, accountId, accountName));
            return accountId;
        }

        public async Task<bool> Delete(int accountId)
        {
            if (!accounts.Any(x => x.AccountId == accountId))
                return false;

            accounts.RemoveAll(x => x.AccountId == accountId);

            return true;
        }

        public IEnumerable<AvailableAccount> GetAvailableAccounts(int userId) =>
            accounts.Where(x => x.UserId == userId).Select(x => new AvailableAccount(x.AccountId, x.Name));

        public async Task<StockAccount?> Get(int accountId)
        {
            var accountToReturn = accounts.FirstOrDefault(x => x.AccountId == accountId);
            if (accountToReturn is null) return null;
            return new StockAccount(accountToReturn.UserId, accountToReturn.AccountId, accountToReturn.Name);
        }
        public int? GetLastAccountId()
        {
            if (accounts.Count != 0)
                return accounts.Max(x => x.AccountId);
            return null;
        }
        public async Task<bool> Update(int accountId, string accountName)
        {
            var bankAccount = accounts.FirstOrDefault(x => x.AccountId == accountId);
            if (bankAccount == null) return false;

            bankAccount.Name = accountName;

            return true;
        }

    }
}
