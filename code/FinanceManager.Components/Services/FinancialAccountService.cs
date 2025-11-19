using FinanceManager.Application.Commands.Account;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

public class FinancialAccountService(BankAccountHttpClient BankAccountHttpClient, StockAccountHttpClient StockAccountHttpClient,
    ILogger<FinancialAccountService> logger) : IFinancialAccountService
{
    public async Task<bool> AccountExists(int id)
    {
        if ((await BankAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;
        if ((await StockAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;

        return false;
    }
    public async Task<bool> AccountExists<T>(int id)
    {
        if (typeof(T) == typeof(BankAccount)) return (await BankAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);
        if (typeof(T) == typeof(StockAccount)) return (await StockAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);

        return false;
    }
    public async Task AddAccount<T>(T account) where T : BasicAccountInformation
    {
        switch (account)
        {
            case BankAccount:
                await BankAccountHttpClient.AddAccountAsync(new AddAccount(account.Name));
                break;
            case StockAccount:
                await StockAccountHttpClient.AddAccountAsync(new AddAccount(account.Name));
                break;
        }
    }
    public async Task AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data)
        where AccountType : BasicAccountInformation
        where EntryType : FinancialEntryBase
    {
        if (typeof(AccountType) == typeof(BankAccount))
        {
            await BankAccountHttpClient.AddAccountAsync(new(accountName));
            foreach (var item in data)
            {
                if (item is BankAccountEntry bankEntry)
                {
                    await BankAccountHttpClient.AddEntryAsync(new(bankEntry.AccountId, bankEntry.EntryId, bankEntry.PostingDate,
                        bankEntry.Value, bankEntry.ValueChange, bankEntry.Description));
                }
            }
        }
        else if (typeof(AccountType) == typeof(StockAccount))
        {
            await StockAccountHttpClient.AddAccountAsync(new(accountName));

            foreach (var item in data)
                if (item is StockAccountEntry stockEntry)
                    await StockAccountHttpClient.AddEntryAsync(new(stockEntry));
        }
    }
    public async Task AddEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        switch (accountEntry)
        {
            case BankAccountEntry bankEntry:
                await BankAccountHttpClient.AddEntryAsync(new(bankEntry.AccountId, bankEntry.EntryId, bankEntry.PostingDate, bankEntry.Value,
                    bankEntry.ValueChange, bankEntry.Description));
                break;

            case StockAccountEntry stockAccountEntry:
                if (!await StockAccountHttpClient.AddEntryAsync(new(stockAccountEntry)))
                    throw new Exception("Adding entry failed");
                break;

        }
    }
    public async Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        if (typeof(T) == typeof(BankAccount))
            return await BankAccountHttpClient.GetAccountWithEntriesAsync(id, dateStart, dateEnd) as T;
        else if (typeof(T) == typeof(StockAccount))
            return await StockAccountHttpClient.GetAccountWithEntriesAsync(id, dateStart, dateEnd) as T;

        return null;
    }
    public async Task<IEnumerable<T>> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        List<T> result = [];
        IEnumerable<AvailableAccount> accounts = [];

        if (typeof(T) == typeof(BankAccount))
            accounts = await BankAccountHttpClient.GetAvailableAccountsAsync();
        else if (typeof(T) == typeof(StockAccount))
            accounts = await StockAccountHttpClient.GetAvailableAccountsAsync();

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

        foreach (var account in await BankAccountHttpClient.GetAvailableAccountsAsync())
            result.Add(account.AccountId, typeof(BankAccount));

        foreach (var account in await StockAccountHttpClient.GetAvailableAccountsAsync())
            result.Add(account.AccountId, typeof(StockAccount));

        return result;
    }
    public async Task<DateTime?> GetEndDate(int accountId)
    {
        if ((await BankAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await BankAccountHttpClient.GetYoungestEntryDate(accountId);

        if ((await StockAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await StockAccountHttpClient.GetYoungestEntryDate(accountId);

        return null;
    }
    public async Task<DateTime?> GetStartDate(int accountId)
    {
        if ((await BankAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await BankAccountHttpClient.GetOldestEntryDate(accountId);

        if ((await StockAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await StockAccountHttpClient.GetOldestEntryDate(accountId);

        return null;
    }
    public async Task<int?> GetLastAccountId()
    {
        var accounts = (await BankAccountHttpClient.GetAvailableAccountsAsync()).ToList();
        accounts.AddRange((await StockAccountHttpClient.GetAvailableAccountsAsync()).ToList());

        if (accounts.Count == 0) return 0;

        return accounts.Max(x => x.AccountId);
    }

    public void InitializeMock() => logger.LogInformation("InitializeMock is called.");
    public async Task RemoveAccount(int id)
    {
        if (await AccountExists<BankAccount>(id))
            await BankAccountHttpClient.DeleteAccountAsync(id);

        if (await AccountExists<StockAccount>(id))
            await StockAccountHttpClient.DeleteAccountAsync(id);
    }
    public async Task RemoveEntry(int entryId, int accountId)
    {
        if (await AccountExists<BankAccount>(accountId))
            await BankAccountHttpClient.DeleteEntryAsync(accountId, entryId);

        if (await AccountExists<StockAccount>(accountId))
            await StockAccountHttpClient.DeleteEntryAsync(accountId, entryId);
    }
    public async Task UpdateAccount<T>(T account) where T : BasicAccountInformation
    {
        if (account is BankAccount bankAccount)
            await BankAccountHttpClient.UpdateAccountAsync(new(bankAccount.AccountId, bankAccount.Name, bankAccount.AccountType));

        if (account is StockAccount)
            await StockAccountHttpClient.UpdateAccountAsync(new(account.AccountId, account.Name));
    }
    public async Task UpdateEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        if (accountEntry is BankAccountEntry bankAccountEntry)
        {
            await BankAccountHttpClient.UpdateEntryAsync(bankAccountEntry);
        }
        else if (accountEntry is StockAccountEntry stockAccountEntry)
        {
            List<UpdateFiancialLabel> labels = [];

            if (stockAccountEntry.Labels is not null && stockAccountEntry.Labels.Count != 0)
                labels = stockAccountEntry.Labels.Select(x => new UpdateFiancialLabel(x.Id, x.Name)).ToList();

            UpdateStockAccountEntry updateStockAccountEntry = new(stockAccountEntry.AccountId, stockAccountEntry.EntryId,
                stockAccountEntry.PostingDate, stockAccountEntry.Value, stockAccountEntry.ValueChange, stockAccountEntry.Ticker,
                stockAccountEntry.InvestmentType, labels);

            await StockAccountHttpClient.UpdateEntryAsync(updateStockAccountEntry);
        }
    }
}