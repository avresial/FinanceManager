using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;

namespace FinanceManager.Infrastructure.Repositories.Account
{
    internal class InMemoryStockAccountRepository : IAccountRepository<StockAccount>
    {
        private List<StockAccount> _accounts = [];

        public async Task<int> GetAccountsCount()
        {
            return await Task.FromResult(_accounts.Count);
        }
        public async Task<int?> Add(int userId, int accountId, string accountName)
        {
            _accounts.Add(new StockAccount(userId, accountId, accountName));
            return await Task.FromResult(accountId);
        }

        public async Task<bool> Delete(int accountId)
        {
            if (!_accounts.Any(x => x.AccountId == accountId))
                return false;

            _accounts.RemoveAll(x => x.AccountId == accountId);

            return await Task.FromResult(true);
        }

        public async Task<IEnumerable<AvailableAccount>> GetAvailableAccounts(int userId) =>
            await Task.FromResult(_accounts
                                    .Where(x => x.UserId == userId)
                                    .Select(x => new AvailableAccount(x.AccountId, x.Name)));

        public async Task<StockAccount?> Get(int accountId)
        {
            var accountToReturn = _accounts.FirstOrDefault(x => x.AccountId == accountId);
            if (accountToReturn is null) return null;
            return await Task.FromResult(new StockAccount(accountToReturn.UserId, accountToReturn.AccountId, accountToReturn.Name));
        }
        public async Task<int?> GetLastAccountId()
        {
            if (_accounts.Count != 0)
                return await Task.FromResult(_accounts.Max(x => x.AccountId));
            return null;
        }
        public async Task<bool> Update(int accountId, string accountName)
        {
            var bankAccount = _accounts.FirstOrDefault(x => x.AccountId == accountId);
            if (bankAccount == null) return false;

            bankAccount.Name = accountName;

            return await Task.FromResult(true);
        }

    }
}
