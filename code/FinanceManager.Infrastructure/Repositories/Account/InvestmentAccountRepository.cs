using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account;

internal class InvestmentAccountRepository(AppDbContext context) : IAccountRepository<InvestmentAccount>
{
    public Task<int> GetAccountsCount() => context.Accounts.AsNoTracking().Where(x => x.AccountType == AccountType.Stock).CountAsync();
    public async Task<int?> Add(int userId, string accountName)
    {
        var result = context.Accounts.Add(new FinancialAccountBaseDto
        {
            UserId = userId,
            Name = accountName,
            AccountType = AccountType.Stock
        });

        await context.SaveChangesAsync();
        if (result is null || result.Entity is null) return null;
        return result.Entity.AccountId;
    }

    public async Task<bool> Delete(int accountId)
    {
        if (context.Database.IsRelational())
        {
            var deleted = await context.Accounts
                .Where(x => x.AccountId == accountId)
                .ExecuteDeleteAsync();
            return deleted > 0;
        }

        var toRemove = await context.Accounts.Where(x => x.AccountId == accountId).ToListAsync();
        if (toRemove.Count == 0) return false;
        context.Accounts.RemoveRange(toRemove);
        await context.SaveChangesAsync();
        return true;
    }

    public IAsyncEnumerable<AvailableAccount> GetAvailableAccounts(int userId) => context.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.AccountType == AccountType.Stock)
            .Select(x => new AvailableAccount(x.AccountId, x.Name))
            .AsAsyncEnumerable();

    public Task<bool> Exists(int accountId) => context.Accounts.AsNoTracking().AnyAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Stock);

    public async Task<List<InvestmentAccount>> GetAll(int userId)
    {
        var accounts = await context.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.AccountType == AccountType.Stock)
            .ToListAsync();

        return accounts
            .Select(x => new InvestmentAccount(x.UserId, x.AccountId, x.Name))
            .ToList();
    }

    public async Task<InvestmentAccount?> Get(int accountId)
    {
        var accountToReturn = await context.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Stock);
        if (accountToReturn is null) return null;
        return new InvestmentAccount(accountToReturn.UserId, accountToReturn.AccountId, accountToReturn.Name);
    }
    public Task<int?> GetLastAccountId() =>
        context.Accounts.AsNoTracking().Where(x => x.AccountType == AccountType.Stock).MaxAsync(x => (int?)x.AccountId);
    public async Task<bool> Update(int accountId, string accountName)
    {
        var investmentAccount = await context.Accounts.FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Stock);
        if (investmentAccount is null) return false;
        investmentAccount.Name = accountName;
        await context.SaveChangesAsync();
        return true;
    }
}