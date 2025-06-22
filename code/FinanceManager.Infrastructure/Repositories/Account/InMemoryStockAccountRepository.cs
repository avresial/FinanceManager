using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account
{
    internal class InMemoryStockAccountRepository(AppDbContext context) : IAccountRepository<StockAccount>
    {
        private readonly AppDbContext _dbContext = context;

        public async Task<int> GetAccountsCount()
        {
            return await _dbContext.Accounts.Where(x => x.AccountType == AccountType.Stock).CountAsync();
        }
        public async Task<int?> Add(int userId, int accountId, string accountName)
        {
            var result = _dbContext.Accounts.Add(new FinancialAccountBaseDto
            {
                UserId = userId,
                AccountId = 0,
                Name = accountName,
                AccountType = AccountType.Stock
            });

            await _dbContext.SaveChangesAsync();
            if (result is null || result.Entity is null) return null;
            return result.Entity.AccountId;
        }

        public async Task<bool> Delete(int accountId)
        {
            var toRemove = await _dbContext.Accounts.Where(x => x.AccountId == accountId).ToListAsync();
            if (toRemove.Count == 0) return false;
            _dbContext.Accounts.RemoveRange(toRemove);
            await _dbContext.SaveChangesAsync();
            return true;
        }

        public async Task<IEnumerable<AvailableAccount>> GetAvailableAccounts(int userId) =>
            await _dbContext.Accounts
                .Where(x => x.UserId == userId && x.AccountType == AccountType.Stock)
                .Select(x => new AvailableAccount(x.AccountId, x.Name))
                .ToListAsync();
        public Task<bool> Exists(int accountId) => _dbContext.Accounts.AnyAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Stock);

        public async Task<StockAccount?> Get(int accountId)
        {
            var accountToReturn = await _dbContext.Accounts.FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Stock);
            if (accountToReturn is null) return null;
            return new StockAccount(accountToReturn.UserId, accountToReturn.AccountId, accountToReturn.Name);
        }
        public async Task<int?> GetLastAccountId()
        {
            if (await _dbContext.Accounts.AnyAsync())
                return await _dbContext.Accounts.MaxAsync(x => x.AccountId);
            return null;
        }
        public async Task<bool> Update(int accountId, string accountName)
        {
            var stockAccount = await _dbContext.Accounts.FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Stock);
            if (stockAccount == null) return false;
            stockAccount.Name = accountName;
            await _dbContext.SaveChangesAsync();
            return true;
        }
    }
}
