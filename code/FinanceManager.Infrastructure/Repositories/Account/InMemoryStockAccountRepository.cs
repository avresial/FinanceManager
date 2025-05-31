using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account
{
    internal class InMemoryStockAccountRepository(StockAccountContext stockAccountContext) : IAccountRepository<StockAccount>
    {
        private readonly StockAccountContext _stockAccountContext = stockAccountContext;

        public async Task<int> GetAccountsCount()
        {
            return await _stockAccountContext.StockAccounts.CountAsync();
        }
        public async Task<int?> Add(int userId, int accountId, string accountName)
        {
            _stockAccountContext.StockAccounts.Add(new FinancialAccountBaseDto
            {
                UserId = userId,
                AccountId = accountId,
                Name = accountName
            });
            await _stockAccountContext.SaveChangesAsync();
            return accountId;
        }

        public async Task<bool> Delete(int accountId)
        {
            var toRemove = await _stockAccountContext.StockAccounts.Where(x => x.AccountId == accountId).ToListAsync();
            if (toRemove.Count == 0) return false;
            _stockAccountContext.StockAccounts.RemoveRange(toRemove);
            await _stockAccountContext.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<AvailableAccount>> GetAvailableAccounts(int userId) =>
            await _stockAccountContext.StockAccounts
                .Where(x => x.UserId == userId)
                .Select(x => new AvailableAccount(x.AccountId, x.Name))
                .ToListAsync();

        public async Task<StockAccount?> Get(int accountId)
        {
            var accountToReturn = await _stockAccountContext.StockAccounts.FirstOrDefaultAsync(x => x.AccountId == accountId);
            if (accountToReturn is null) return null;
            return new StockAccount(accountToReturn.UserId, accountToReturn.AccountId, accountToReturn.Name);
        }
        public async Task<int?> GetLastAccountId()
        {
            if (await _stockAccountContext.StockAccounts.AnyAsync())
                return await _stockAccountContext.StockAccounts.MaxAsync(x => x.AccountId);
            return null;
        }
        public async Task<bool> Update(int accountId, string accountName)
        {
            var stockAccount = await _stockAccountContext.StockAccounts.FirstOrDefaultAsync(x => x.AccountId == accountId);
            if (stockAccount == null) return false;
            stockAccount.Name = accountName;
            await _stockAccountContext.SaveChangesAsync();
            return true;
        }
    }
}
