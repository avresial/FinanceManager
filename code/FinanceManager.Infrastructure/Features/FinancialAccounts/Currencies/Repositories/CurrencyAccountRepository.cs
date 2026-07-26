using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Features.FinancialAccounts.Currencies.Repositories;

internal class CurrencyAccountRepository(AppDbContext context) : ICurrencyAccountRepository<CurrencyAccount>
{

    public Task<int> GetAccountsCount() => context.Accounts.AsNoTracking().CountAsync();

    public Task<int?> GetLastAccountId() =>
        context.Accounts.AsNoTracking().Where(x => x.AccountType == AccountType.Currency).MaxAsync(x => (int?)x.AccountId);
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
        .AsNoTracking()
        .Where(x => x.UserId == userId && x.AccountType == AccountType.Currency)
        .Select(x => new AvailableAccount(x.AccountId, x.Name))
        .AsAsyncEnumerable();

    public Task<bool> Exists(int accountId) => context.Accounts.AsNoTracking().AnyAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Currency);

    public async Task<List<CurrencyAccount>> GetAll(int userId)
    {
        var accounts = await context.Accounts
            .AsNoTracking()
            .Where(x => x.UserId == userId && x.AccountType == AccountType.Currency)
            .ToListAsync();

        return accounts
            .Select(x => new CurrencyAccount(x.UserId, x.AccountId, x.Name, x.AccountLabel))
            .ToList();
    }

    public async Task<CurrencyAccount?> Get(int accountId)
    {
        var accountToReturn = await context.Accounts.AsNoTracking().FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Currency);
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