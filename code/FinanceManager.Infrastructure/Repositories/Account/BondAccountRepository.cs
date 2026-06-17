using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account;

internal class BondAccountRepository(AppDbContext context) : IAccountRepository<BondAccount>
{
    public Task<int> GetAccountsCount() => context.Accounts.AsNoTracking().Where(x => x.AccountType == AccountType.Bond).CountAsync();

    public async Task<int?> Add(int userId, string accountName)
    {
        var result = context.Accounts.Add(new FinancialAccountBaseDto
        {
            UserId = userId,
            Name = accountName,
            AccountType = AccountType.Bond
        });

        await context.SaveChangesAsync();
        if (result is null || result.Entity is null) return null;
        return result.Entity.AccountId;
    }

    public async Task<bool> Delete(int accountId)
    {
        var toRemove = await context.Accounts.Where(x => x.AccountId == accountId && x.AccountType == AccountType.Bond).ToListAsync();
        if (toRemove.Count == 0) return false;
        context.Accounts.RemoveRange(toRemove);
        await context.SaveChangesAsync();
        return true;
    }

    public IAsyncEnumerable<AvailableAccount> GetAvailableAccounts(int userId) => context.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.AccountType == AccountType.Bond)
            .Select(x => new AvailableAccount(x.AccountId, x.Name))
            .AsAsyncEnumerable();

    public Task<bool> Exists(int accountId) => context.Accounts.AsNoTracking().AnyAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Bond);

    public async Task<List<BondAccount>> GetAll(int userId)
    {
        var accounts = await context.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.AccountType == AccountType.Bond)
            .ToListAsync();

        return accounts
            .Select(x => new BondAccount(x.UserId, x.AccountId, x.Name))
            .ToList();
    }

    public async Task<BondAccount?> Get(int accountId)
    {
        var accountToReturn = await context.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Bond);
        if (accountToReturn is null) return null;
        return new BondAccount(accountToReturn.UserId, accountToReturn.AccountId, accountToReturn.Name);
    }

    public Task<int?> GetLastAccountId() =>
        context.Accounts.AsNoTracking().Where(x => x.AccountType == AccountType.Bond).MaxAsync(x => (int?)x.AccountId);

    public async Task<bool> Update(int accountId, string accountName)
    {
        var bondAccount = await context.Accounts.FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Bond);
        if (bondAccount is null) return false;
        bondAccount.Name = accountName;
        await context.SaveChangesAsync();
        return true;
    }
}