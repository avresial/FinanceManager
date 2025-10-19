using FinanceManager.Application.Commands.Account;
using FinanceManager.Components.HttpContexts;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

public class FinancialAccountService(BankAccountHttpContext bankAccountHttpContext, StockAccountHttpContext stockAccountHttpContext,
    ILogger<FinancialAccountService> logger) : IFinancialAccountService
{
    public async Task<bool> AccountExists(int id)
    {
        if ((await bankAccountHttpContext.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;
        if ((await stockAccountHttpContext.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;

        return false;
    }
    public async Task<bool> AccountExists<T>(int id)
    {
        if (typeof(T) == typeof(BankAccount)) return (await bankAccountHttpContext.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);
        if (typeof(T) == typeof(StockAccount)) return (await stockAccountHttpContext.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);

        return false;
    }
    public async Task AddAccount<T>(T account) where T : BasicAccountInformation
    {
        switch (account)
        {
            case BankAccount:
                await bankAccountHttpContext.AddAccountAsync(new AddAccount(account.Name));
                break;
            case StockAccount:
                await stockAccountHttpContext.AddAccountAsync(new AddAccount(account.Name));
                break;
        }
    }
    public async Task AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data)
        where AccountType : BasicAccountInformation
        where EntryType : FinancialEntryBase
    {
        if (typeof(AccountType) == typeof(BankAccount))
        {
            await bankAccountHttpContext.AddAccountAsync(new(accountName));
            foreach (var item in data)
            {
                if (item is BankAccountEntry bankEntry)
                {
                    await bankAccountHttpContext.AddEntryAsync(new(bankEntry.AccountId, bankEntry.EntryId, bankEntry.PostingDate,
                        bankEntry.Value, bankEntry.ValueChange, bankEntry.Description));
                }
            }
        }
        else if (typeof(AccountType) == typeof(StockAccount))
        {
            await stockAccountHttpContext.AddAccountAsync(new(accountName));

            foreach (var item in data)
                if (item is StockAccountEntry stockEntry)
                    await stockAccountHttpContext.AddEntryAsync(new(stockEntry));
        }
    }
    public async Task AddEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        switch (accountEntry)
        {
            case BankAccountEntry bankEntry:
                await bankAccountHttpContext.AddEntryAsync(new(bankEntry.AccountId, bankEntry.EntryId, bankEntry.PostingDate, bankEntry.Value,
                    bankEntry.ValueChange, bankEntry.Description));
                break;

            case StockAccountEntry stockAccountEntry:
                if (!await stockAccountHttpContext.AddEntryAsync(new(stockAccountEntry)))
                    throw new Exception("Adding entry failed");
                break;

        }
    }
    public async Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        if (typeof(T) == typeof(BankAccount))
            return await bankAccountHttpContext.GetAccountWithEntriesAsync(id, dateStart, dateEnd) as T;
        else if (typeof(T) == typeof(StockAccount))
            return await stockAccountHttpContext.GetAccountWithEntriesAsync(id, dateStart, dateEnd) as T;

        return null;
    }
    public async Task<IEnumerable<T>> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        List<T> result = [];
        IEnumerable<AvailableAccount> accounts = [];

        if (typeof(T) == typeof(BankAccount))
            accounts = await bankAccountHttpContext.GetAvailableAccountsAsync();
        else if (typeof(T) == typeof(StockAccount))
            accounts = await stockAccountHttpContext.GetAvailableAccountsAsync();

        foreach (var account in accounts)
        {
            T? nextAccount = await GetAccount<T>(userId, account.AccountId, dateStart, dateEnd);
            if (nextAccount is null) continue;
            result.Add(nextAccount);
        }

        return result;
    }
    public async Task<Dictionary<int, Type>> GetAvailableAccounts()
    {
        Dictionary<int, Type> result = [];

        foreach (var account in await bankAccountHttpContext.GetAvailableAccountsAsync())
            result.Add(account.AccountId, typeof(BankAccount));

        foreach (var account in await stockAccountHttpContext.GetAvailableAccountsAsync())
            result.Add(account.AccountId, typeof(StockAccount));

        return result;
    }
    public async Task<DateTime?> GetEndDate(int accountId)
    {
        if ((await bankAccountHttpContext.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await bankAccountHttpContext.GetYoungestEntryDate(accountId);

        if ((await stockAccountHttpContext.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await stockAccountHttpContext.GetYoungestEntryDate(accountId);

        return null;
    }
    public async Task<DateTime?> GetStartDate(int accountId)
    {
        if ((await bankAccountHttpContext.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await bankAccountHttpContext.GetOldestEntryDate(accountId);

        if ((await stockAccountHttpContext.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await stockAccountHttpContext.GetOldestEntryDate(accountId);

        return null;
    }
    public async Task<int?> GetLastAccountId()
    {
        var accounts = (await bankAccountHttpContext.GetAvailableAccountsAsync()).ToList();
        accounts.AddRange((await stockAccountHttpContext.GetAvailableAccountsAsync()).ToList());

        if (accounts.Count == 0) return 0;

        return accounts.Max(x => x.AccountId);
    }

    public void InitializeMock() => logger.LogInformation("InitializeMock is called.");
    public async Task RemoveAccount(int id)
    {
        if (await AccountExists<BankAccount>(id))
            await bankAccountHttpContext.DeleteAccountAsync(id);

        if (await AccountExists<StockAccount>(id))
            await stockAccountHttpContext.DeleteAccountAsync(id);
    }
    public async Task RemoveEntry(int entryId, int accountId)
    {
        if (await AccountExists<BankAccount>(accountId))
            await bankAccountHttpContext.DeleteEntryAsync(accountId, entryId);

        if (await AccountExists<StockAccount>(accountId))
            await stockAccountHttpContext.DeleteEntryAsync(accountId, entryId);
    }
    public async Task UpdateAccount<T>(T account) where T : BasicAccountInformation
    {
        if (account is BankAccount bankAccount)
            await bankAccountHttpContext.UpdateAccountAsync(new(bankAccount.AccountId, bankAccount.Name, bankAccount.AccountType));

        if (account is StockAccount)
            await stockAccountHttpContext.UpdateAccountAsync(new(account.AccountId, account.Name));
    }
    public async Task UpdateEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        if (accountEntry is BankAccountEntry bankAccountEntry)
        {
            await bankAccountHttpContext.UpdateEntryAsync(bankAccountEntry);
        }
        else if (accountEntry is StockAccountEntry stockAccountEntry)
        {
            List<UpdateFiancialLabel> labels = [];

            if (stockAccountEntry.Labels is not null && stockAccountEntry.Labels.Count != 0)
                labels = stockAccountEntry.Labels.Select(x => new UpdateFiancialLabel(x.Id, x.Name)).ToList();

            UpdateStockAccountEntry updateStockAccountEntry = new(stockAccountEntry.AccountId, stockAccountEntry.EntryId,
                stockAccountEntry.PostingDate, stockAccountEntry.Value, stockAccountEntry.ValueChange, stockAccountEntry.Ticker,
                stockAccountEntry.InvestmentType, labels);

            await stockAccountHttpContext.UpdateEntryAsync(updateStockAccountEntry);
        }
    }
}