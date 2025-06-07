using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account;

internal class InMemoryBankAccountRepository(AppDbContext context) : IBankAccountRepository<BankAccount>
{
    private readonly AppDbContext _dbContext = context;

    public async Task<int> GetAccountsCount() => await _dbContext.BankAccounts.CountAsync();

    public async Task<int?> GetLastAccountId()
    {
        if (await _dbContext.BankAccounts.AnyAsync())
            return await _dbContext.BankAccounts.MaxAsync(x => x.AccountId);

        return null;
    }
    public async Task<int?> Add(int userId, int accountId, string accountName) => await Add(userId, accountId, accountName, AccountLabel.Other);
    public async Task<int?> Add(int userId, int accountId, string accountName, AccountLabel accountLabel)
    {
        var result = _dbContext.BankAccounts.Add(new FinancialAccountBaseDto
        {
            UserId = userId,
            AccountId = 0,
            Name = accountName,
            AccountLabel = accountLabel,
            AccountType = AccountType.Bank
        });

        await _dbContext.SaveChangesAsync();
        if (result is null || result.Entity is null) return null;
        return result.Entity.AccountId;
    }
    public async Task<bool> Delete(int accountId)
    {
        var toRemove = await _dbContext.BankAccounts.Where(x => x.AccountId == accountId && x.AccountType == AccountType.Bank).ToListAsync();
        if (toRemove.Count == 0) return false;

        _dbContext.BankAccounts.RemoveRange(toRemove);
        await _dbContext.SaveChangesAsync();

        return true;
    }
    public async Task<IEnumerable<AvailableAccount>> GetAvailableAccounts(int userId) =>
        await _dbContext.BankAccounts
        .Where(x => x.UserId == userId && x.AccountType == AccountType.Bank)
        .Select(x => new AvailableAccount(x.AccountId, x.Name))
        .ToListAsync();

    public Task<bool> Exists(int accountId) => _dbContext.BankAccounts.AnyAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Bank);

    public async Task<BankAccount?> Get(int accountId)
    {
        var accountToReturn = await _dbContext.BankAccounts.FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Bank);
        if (accountToReturn is null) return null;
        return new BankAccount(accountToReturn.UserId, accountToReturn.AccountId, accountToReturn.Name, accountToReturn.AccountLabel);
    }

    public async Task<bool> Update(int accountId, string accountName)
    {
        var bankAccount = await _dbContext.BankAccounts.FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Bank);
        if (bankAccount == null) return false;
        bankAccount.Name = accountName;
        await _dbContext.SaveChangesAsync();
        return true;
    }
    public async Task<bool> Update(int accountId, string accountName, AccountLabel accountType)
    {
        var bankAccount = await _dbContext.BankAccounts.FirstOrDefaultAsync(x => x.AccountId == accountId && x.AccountType == AccountType.Bank);
        if (bankAccount == null) return false;
        bankAccount.Name = accountName;
        bankAccount.AccountLabel = accountType;
        await _dbContext.SaveChangesAsync();
        return true;
    }


}
