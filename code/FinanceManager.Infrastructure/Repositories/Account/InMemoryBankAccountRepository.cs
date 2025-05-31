using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.ValueObjects;
using FinanceManager.Infrastructure.Contexts;
using FinanceManager.Infrastructure.Dtos;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories.Account;

internal class InMemoryBankAccountRepository(BankAccountContext bankAccountContext) : IBankAccountRepository<BankAccount>
{
    private readonly BankAccountContext _bankAccountContext = bankAccountContext;

    public async Task<int> GetAccountsCount() => await _bankAccountContext.BankAccounts.CountAsync();

    public async Task<int?> GetLastAccountId()
    {
        if (await _bankAccountContext.BankAccounts.AnyAsync())
            return await _bankAccountContext.BankAccounts.MaxAsync(x => x.AccountId);

        return null;
    }
    public async Task<int?> Add(int userId, int accountId, string accountName) => await Add(userId, accountId, accountName, AccountType.Other);
    public async Task<int?> Add(int userId, int accountId, string accountName, AccountType accountType)
    {
        _bankAccountContext.BankAccounts.Add(new FinancialAccountBaseDto
        {
            UserId = userId,
            AccountId = accountId,
            Name = accountName,
            AccountType = accountType
        });

        await _bankAccountContext.SaveChangesAsync();
        return accountId;
    }
    public async Task<bool> Delete(int accountId)
    {
        var toRemove = await _bankAccountContext.BankAccounts.Where(x => x.AccountId == accountId).ToListAsync();
        if (toRemove.Count == 0) return false;

        _bankAccountContext.BankAccounts.RemoveRange(toRemove);
        await _bankAccountContext.SaveChangesAsync();

        return true;
    }
    public async Task<IEnumerable<AvailableAccount>> GetAvailableAccounts(int userId) =>
        await _bankAccountContext.BankAccounts
        .Where(x => x.UserId == userId)
        .Select(x => new AvailableAccount(x.AccountId, x.Name))
        .ToListAsync();

    public async Task<BankAccount?> Get(int accountId)
    {
        var accountToReturn = await _bankAccountContext.BankAccounts.FirstOrDefaultAsync(x => x.AccountId == accountId);
        if (accountToReturn is null) return null;
        return new BankAccount(accountToReturn.UserId, accountToReturn.AccountId, accountToReturn.Name, accountToReturn.AccountType);
    }

    public async Task<bool> Update(int accountId, string accountName)
    {
        var bankAccount = await _bankAccountContext.BankAccounts.FirstOrDefaultAsync(x => x.AccountId == accountId);
        if (bankAccount == null) return false;
        bankAccount.Name = accountName;
        await _bankAccountContext.SaveChangesAsync();
        return true;
    }
    public async Task<bool> Update(int accountId, string accountName, AccountType accountType)
    {
        var bankAccount = await _bankAccountContext.BankAccounts.FirstOrDefaultAsync(x => x.AccountId == accountId);
        if (bankAccount == null) return false;
        bankAccount.Name = accountName;
        bankAccount.AccountType = accountType;
        await _bankAccountContext.SaveChangesAsync();
        return true;
    }
}
