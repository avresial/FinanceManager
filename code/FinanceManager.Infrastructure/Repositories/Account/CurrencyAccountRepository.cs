using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account;

internal class CurrencyAccountRepository(AppDbContext context) : ICurrencyAccountRepository<CurrencyAccount>
{

    public Task<int> GetAccountsCount() => context.Accounts.CountAsync();

    public async Task<int?> GetLastAccountId()
    {
        if (await context.Accounts.AnyAsync())
            return await context.Accounts.MaxAsync(x => x.AccountId);

        return null;
    }
    public Task<int?> Add(int userId, string accountName) => Add(userId, accountName, AccountLabel.Other);
    public async Task<int?> Add(int userId, string accountName, AccountLabel accountLabel)
    {
        var result = context.Accounts.Add(new FinancialAccountBaseDto
        {
            UserId = userId,
            AccountId = 0,
            Name = accountName,
            AccountLabel = accountLabel,
            AccountType = AccountType.Currency
        });

        await context.SaveChangesAsync();
        if (result is null || result.Entity is null) return null;
        return result.Entity.AccountId;
    }
    public async Task<bool> Delete(int accountId)
    {
        var toRemove = await context.Accounts.Where(x => x.AccountId == accountId && x.AccountType == AccountType.Currency).ToListAsync();
        if (toRemove.Count == 0) return false;

        context.Accounts.RemoveRange(toRemove);
        await context.SaveChangesAsync();

        return true;
    }
    public IAsyncEnumerable<AvailableAccount> GetAvailableAccounts(int userId) => context.Accounts
        .Where(x => x.UserId == userId && x.AccountType == AccountType.Currency)
        .Select(x => new AvailableAccount(x.AccountId, x.Name))
        .AsAsyncEnumerable();

    public Task<bool> Exists(int accountId) => context.Accounts.AnyAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Currency);

    public async Task<CurrencyAccount?> Get(int accountId)
    {
        var accountToReturn = await context.Accounts.FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Currency);
        if (accountToReturn is null) return null;
        return new CurrencyAccount(accountToReturn.UserId, accountToReturn.AccountId, accountToReturn.Name, accountToReturn.AccountLabel);
    }

    public async Task<bool> Update(int accountId, string accountName)
    {
        var account = await context.Accounts.FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Currency);
        if (account is null) return false;
        account.Name = accountName;
        await context.SaveChangesAsync();
        return true;
    }
    public async Task<bool> Update(int accountId, string accountName, AccountLabel accountType)
    {
        var account = await context.Accounts.FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Currency);
        if (account is null) return false;
        account.Name = accountName;
        account.AccountLabel = accountType;
        await context.SaveChangesAsync();
        return true;
    }
}