using FinanceManager.Application.Commands.Account;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

public class FinancialAccountService(BankAccountHttpClient bankAccountHttpClient, BankEntryHttpClient bankEntryHttpClient,
    StockAccountHttpClient stockAccountHttpClient, ILogger<FinancialAccountService> logger) : IFinancialAccountService
{
    public async Task<bool> AccountExists(int id)
    {
        if ((await bankAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;
        if ((await stockAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;

        return false;
    }
    public async Task<bool> AccountExists<T>(int id)
    {
        if (typeof(T) == typeof(BankAccount)) return (await bankAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);
        if (typeof(T) == typeof(StockAccount)) return (await stockAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);

        return false;
    }
    public async Task AddAccount<T>(T account) where T : BasicAccountInformation
    {
        switch (account)
        {
            case BankAccount:
                await bankAccountHttpClient.AddAccountAsync(new AddAccount(account.Name));
                break;
            case StockAccount:
                await stockAccountHttpClient.AddAccountAsync(new AddAccount(account.Name));
                break;
        }
    }
    public async Task AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data)
        where AccountType : BasicAccountInformation
        where EntryType : FinancialEntryBase
    {
        if (typeof(AccountType) == typeof(BankAccount))
        {
            await bankAccountHttpClient.AddAccountAsync(new(accountName));
            foreach (var item in data)
            {
                if (item is BankAccountEntry bankEntry)
                {
                    await bankEntryHttpClient.AddEntryAsync(new(bankEntry.AccountId, bankEntry.EntryId, bankEntry.PostingDate,
                        bankEntry.Value, bankEntry.ValueChange, bankEntry.Description));
                }
            }
        }
        else if (typeof(AccountType) == typeof(StockAccount))
        {
            await stockAccountHttpClient.AddAccountAsync(new(accountName));

            foreach (var item in data)
                if (item is StockAccountEntry stockEntry)
                    await stockAccountHttpClient.AddEntryAsync(new(stockEntry));
        }
    }
    public async Task AddEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        switch (accountEntry)
        {
            case BankAccountEntry bankEntry:
                await bankEntryHttpClient.AddEntryAsync(new(bankEntry.AccountId, bankEntry.EntryId, bankEntry.PostingDate, bankEntry.Value,
                    bankEntry.ValueChange, bankEntry.Description));
                break;

            case StockAccountEntry stockAccountEntry:
                if (!await stockAccountHttpClient.AddEntryAsync(new(stockAccountEntry)))
                    throw new Exception("Adding entry failed");
                break;

        }
    }
    public async Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        if (typeof(T) == typeof(BankAccount))
            return await bankAccountHttpClient.GetAccountWithEntriesAsync(id, dateStart, dateEnd) as T;
        else if (typeof(T) == typeof(StockAccount))
            return await stockAccountHttpClient.GetAccountWithEntriesAsync(id, dateStart, dateEnd) as T;

        return null;
    }
    public async Task<IEnumerable<T>> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        List<T> result = [];
        IEnumerable<AvailableAccount> accounts = [];

        if (typeof(T) == typeof(BankAccount))
            accounts = await bankAccountHttpClient.GetAvailableAccountsAsync();
        else if (typeof(T) == typeof(StockAccount))
            accounts = await stockAccountHttpClient.GetAvailableAccountsAsync();

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

        foreach (var account in await bankAccountHttpClient.GetAvailableAccountsAsync())
            result.Add(account.AccountId, typeof(BankAccount));

        foreach (var account in await stockAccountHttpClient.GetAvailableAccountsAsync())
            result.Add(account.AccountId, typeof(StockAccount));

        return result;
    }
    public async Task<DateTime?> GetEndDate(int accountId)
    {
        if ((await bankAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await bankEntryHttpClient.GetYoungestEntryDate(accountId);

        if ((await stockAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await stockAccountHttpClient.GetYoungestEntryDate(accountId);

        return null;
    }
    public async Task<DateTime?> GetStartDate(int accountId)
    {
        if ((await bankAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await bankEntryHttpClient.GetOldestEntryDate(accountId);

        if ((await stockAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await stockAccountHttpClient.GetOldestEntryDate(accountId);

        return null;
    }
    public async Task<int?> GetLastAccountId()
    {
        var accounts = (await bankAccountHttpClient.GetAvailableAccountsAsync()).ToList();
        accounts.AddRange((await stockAccountHttpClient.GetAvailableAccountsAsync()).ToList());

        if (accounts.Count == 0) return 0;

        return accounts.Max(x => x.AccountId);
    }

    public void InitializeMock() => logger.LogInformation("InitializeMock is called.");
    public async Task RemoveAccount(int id)
    {
        if (await AccountExists<BankAccount>(id))
            await bankAccountHttpClient.DeleteAccountAsync(id);

        if (await AccountExists<StockAccount>(id))
            await stockAccountHttpClient.DeleteAccountAsync(id);
    }
    public async Task RemoveEntry(int entryId, int accountId)
    {
        if (await AccountExists<BankAccount>(accountId))
            await bankEntryHttpClient.DeleteEntryAsync(accountId, entryId);

        if (await AccountExists<StockAccount>(accountId))
            await stockAccountHttpClient.DeleteEntryAsync(accountId, entryId);
    }
    public async Task UpdateAccount<T>(T account) where T : BasicAccountInformation
    {
        if (account is BankAccount bankAccount)
            await bankAccountHttpClient.UpdateAccountAsync(new(bankAccount.AccountId, bankAccount.Name, bankAccount.AccountType));

        if (account is StockAccount)
            await stockAccountHttpClient.UpdateAccountAsync(new(account.AccountId, account.Name));
    }
    public async Task UpdateEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        if (accountEntry is BankAccountEntry bankAccountEntry)
        {
            await bankEntryHttpClient.UpdateEntryAsync(bankAccountEntry);
        }
        else if (accountEntry is StockAccountEntry stockAccountEntry)
        {
            List<UpdateFiancialLabel> labels = [];

            if (stockAccountEntry.Labels is not null && stockAccountEntry.Labels.Count != 0)
                labels = stockAccountEntry.Labels.Select(x => new UpdateFiancialLabel(x.Id, x.Name)).ToList();

            UpdateStockAccountEntry updateStockAccountEntry = new(stockAccountEntry.AccountId, stockAccountEntry.EntryId,
                stockAccountEntry.PostingDate, stockAccountEntry.Value, stockAccountEntry.ValueChange, stockAccountEntry.Ticker,
                stockAccountEntry.InvestmentType, labels);

            await stockAccountHttpClient.UpdateEntryAsync(updateStockAccountEntry);
        }
    }
}