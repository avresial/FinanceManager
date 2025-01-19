﻿using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories
{
    internal class InMemoryStockAccountRepository : IAccountRepository<StockAccount>
    {
        private List<StockAccount> _bankAccounts = new List<StockAccount>();

        public bool Add(int userId, string accountName)
        {
            _bankAccounts.Add(new StockAccount(userId, _bankAccounts.Count + 1, accountName));
            return true;
        }

        public bool Delete(int accountId)
        {
            if (!_bankAccounts.Any(x => x.Id == accountId))
                return false;

            _bankAccounts.RemoveAll(x => x.Id == accountId);

            return true;
        }

        public IList<(int, string)> GetAvailableAccounts(int accountId) => _bankAccounts.Where(x => x.UserId == accountId).Select(x => (x.Id, x.Name)).ToList();

        public StockAccount? Get(int accountId) => _bankAccounts.FirstOrDefault(x => x.Id == accountId);

        public bool Update(int accountId, string accountName)
        {
            var bankAccount = _bankAccounts.FirstOrDefault(x => x.Id == accountId);
            if (bankAccount == null) return false;

            bankAccount.Name = accountName;

            return true;
        }
    }
}
