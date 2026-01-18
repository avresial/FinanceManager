using FinanceManager.Application.Commands.Account;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

public class FinancialAccountService(CurrencyAccountHttpClient bankAccountHttpClient, CurrencyEntryHttpClient bankEntryHttpClient,
    StockAccountHttpClient stockAccountHttpClient, StockEntryHttpClient stockEntryHttpClient,
    BondAccountHttpClient bondAccountHttpClient, BondEntryHttpClient bondEntryHttpClient,
    ILogger<FinancialAccountService> logger) : IFinancialAccountService
{
    public async Task<bool> AccountExists(int id)
    {
        if ((await bankAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;
        if ((await stockAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;
        if ((await bondAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;

        return false;
    }
    public async Task<bool> AccountExists<T>(int id)
    {
        if (typeof(T) == typeof(CurrencyAccount)) return (await bankAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);
        if (typeof(T) == typeof(StockAccount)) return (await stockAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);
        if (typeof(T) == typeof(BondAccount)) return (await bondAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);

        return false;
    }
    public async Task AddAccount<T>(T account) where T : BasicAccountInformation
    {
        switch (account)
        {
            case CurrencyAccount:
                await bankAccountHttpClient.AddAccountAsync(new AddAccount(account.Name));
                break;
            case StockAccount:
                await stockAccountHttpClient.AddAccountAsync(new AddAccount(account.Name));
                break;
            case BondAccount:
                await bondAccountHttpClient.AddAccountAsync(new AddBondAccount(account.Name));
                break;

            default: throw new NotSupportedException($"Account type {typeof(T)} not supported for adding account.");
        }
    }
    public async Task AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data)
        where AccountType : BasicAccountInformation
        where EntryType : FinancialEntryBase
    {
        if (typeof(AccountType) == typeof(CurrencyAccount))
        {
            await bankAccountHttpClient.AddAccountAsync(new(accountName));
            foreach (var item in data)
            {
                if (item is CurrencyAccountEntry bankEntry)
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
                    await stockEntryHttpClient.AddEntryAsync(new(stockEntry));
        }
        else if (typeof(AccountType) == typeof(BondAccount))
        {
            await bondAccountHttpClient.AddAccountAsync(new(accountName));
            foreach (var item in data)
                if (item is BondAccountEntry bondEntry)
                    await bondEntryHttpClient.AddEntryAsync(new(bondEntry.AccountId, bondEntry.EntryId, bondEntry.PostingDate,
                        bondEntry.Value, bondEntry.ValueChange, bondEntry.BondDetailsId));
        }
        else
        {
            throw new NotSupportedException($"Account type {typeof(AccountType)} not supported for adding account with entries.");
        }
    }
    public async Task AddEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        switch (accountEntry)
        {
            case CurrencyAccountEntry bankEntry:
                await bankEntryHttpClient.AddEntryAsync(new(bankEntry.AccountId, bankEntry.EntryId, bankEntry.PostingDate, bankEntry.Value,
                    bankEntry.ValueChange, bankEntry.Description));
                break;

            case StockAccountEntry stockAccountEntry:
                if (!await stockEntryHttpClient.AddEntryAsync(new(stockAccountEntry)))
                    throw new Exception("Adding entry failed");
                break;

            case BondAccountEntry bondEntry:
                await bondEntryHttpClient.AddEntryAsync(new(bondEntry.AccountId, bondEntry.EntryId, bondEntry.PostingDate,
                    bondEntry.Value, bondEntry.ValueChange, bondEntry.BondDetailsId));
                break;

            default: throw new NotSupportedException($"Account entry {accountEntry.GetType()} type not supported for adding entry.");

        }
    }
    public async Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        if (typeof(T) == typeof(CurrencyAccount))
            return await bankAccountHttpClient.GetAccountWithEntriesAsync(id, dateStart, dateEnd) as T;
        else if (typeof(T) == typeof(StockAccount))
            return await stockAccountHttpClient.GetAccountWithEntriesAsync(id, dateStart, dateEnd) as T;
        else if (typeof(T) == typeof(BondAccount))
            return await bondAccountHttpClient.GetAccountWithEntriesAsync(id, dateStart, dateEnd) as T;

        throw new NotSupportedException($"Account type {typeof(T)} not supported for getting start date.");

    }
    public async Task<IEnumerable<T>> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        List<T> result = [];
        IEnumerable<AvailableAccount> accounts = [];

        if (typeof(T) == typeof(CurrencyAccount))
            accounts = await bankAccountHttpClient.GetAvailableAccountsAsync();
        else if (typeof(T) == typeof(StockAccount))
            accounts = await stockAccountHttpClient.GetAvailableAccountsAsync();
        else if (typeof(T) == typeof(BondAccount))
            accounts = await bondAccountHttpClient.GetAvailableAccountsAsync();
        else
            throw new NotSupportedException($"Account type {typeof(T)} not supported for getting start date.");

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
        try
        {
            foreach (var account in await bankAccountHttpClient.GetAvailableAccountsAsync())
                result.Add(account.AccountId, typeof(CurrencyAccount));
        }
        catch (Exception ex)
        {
            logger.LogError("Error while fetching bank accounts: {Message}", ex.Message);
        }

        try
        {
            foreach (var account in await stockAccountHttpClient.GetAvailableAccountsAsync())
                result.Add(account.AccountId, typeof(StockAccount));
        }
        catch (Exception ex)
        {
            logger.LogError("Error while fetching stock accounts: {Message}", ex.Message);
        }

        try
        {
            foreach (var account in await bondAccountHttpClient.GetAvailableAccountsAsync())
                result.Add(account.AccountId, typeof(BondAccount));
        }
        catch (Exception ex)
        {
            logger.LogError("Error while fetching stock accounts: {Message}", ex.Message);
        }

        return result;
    }
    public async Task<DateTime?> GetEndDate(int accountId)
    {
        if ((await bankAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await bankEntryHttpClient.GetYoungestEntryDate(accountId);

        if ((await stockAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await stockEntryHttpClient.GetYoungestEntryDate(accountId);

        if ((await bondAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await bondEntryHttpClient.GetYoungestEntryDate(accountId);

        throw new NotSupportedException($"Account {accountId} type not supported for getting start date.");
    }
    public async Task<DateTime?> GetStartDate(int accountId)
    {
        if ((await bankAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await bankEntryHttpClient.GetOldestEntryDate(accountId);

        if ((await stockAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await stockEntryHttpClient.GetOldestEntryDate(accountId);

        if ((await bondAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await bondEntryHttpClient.GetOldestEntryDate(accountId);

        throw new NotSupportedException($"Account {accountId} type not supported for getting start date.");
    }
    public async Task<int?> GetLastAccountId()
    {
        List<AvailableAccount> accounts = [.. (await bankAccountHttpClient.GetAvailableAccountsAsync())];
        accounts.AddRange([.. (await stockAccountHttpClient.GetAvailableAccountsAsync())]);
        accounts.AddRange([.. (await bondAccountHttpClient.GetAvailableAccountsAsync())]);

        if (accounts.Count == 0) return 0;

        return accounts.Max(x => x.AccountId);
    }

    public void InitializeMock() => logger.LogInformation("InitializeMock is called.");
    public async Task RemoveAccount(int id)
    {
        if (await AccountExists<CurrencyAccount>(id))
        {
            await bankAccountHttpClient.DeleteAccountAsync(id);
            return;
        }

        if (await AccountExists<StockAccount>(id))
        {
            await stockAccountHttpClient.DeleteAccountAsync(id);
            return;
        }

        if (await AccountExists<BondAccount>(id))
        {
            await bondAccountHttpClient.DeleteAccountAsync(id);
            return;
        }

        throw new NotSupportedException($"Account {id} type not supported for getting start date.");
    }
    public async Task RemoveEntry(int entryId, int accountId)
    {
        if (await AccountExists<CurrencyAccount>(accountId))
            await bankEntryHttpClient.DeleteEntryAsync(accountId, entryId);
        else if (await AccountExists<StockAccount>(accountId))
            await stockEntryHttpClient.DeleteEntryAsync(accountId, entryId);
        else if (await AccountExists<BondAccount>(accountId))
            await bondEntryHttpClient.DeleteEntryAsync(accountId, entryId);

        else throw new InvalidOperationException($"Account {accountId}, entryId {entryId} not found.");
    }
    public Task UpdateAccount<T>(T account) where T : BasicAccountInformation
    {
        if (account is CurrencyAccount bankAccount)
            return bankAccountHttpClient.UpdateAccountAsync(new(bankAccount.AccountId, bankAccount.Name, bankAccount.AccountType));

        if (account is StockAccount)
            return stockAccountHttpClient.UpdateAccountAsync(new(account.AccountId, account.Name, Domain.Enums.AccountLabel.Stock));

        if (account is BondAccount)
            return bondAccountHttpClient.UpdateAccountAsync(new(account.AccountId, account.Name, Domain.Enums.AccountLabel.Bond));

        throw new NotSupportedException($"Account {account.GetType()} type not supported for getting start date.");
    }
    public async Task UpdateEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        if (accountEntry is CurrencyAccountEntry bankAccountEntry)
        {
            await bankEntryHttpClient.UpdateEntryAsync(bankAccountEntry);
            return;
        }
        else if (accountEntry is StockAccountEntry stockAccountEntry)
        {
            List<UpdateFiancialLabel> labels = [];

            if (stockAccountEntry.Labels is not null && stockAccountEntry.Labels.Count != 0)
                labels = stockAccountEntry.Labels.Select(x => new UpdateFiancialLabel(x.Id, x.Name)).ToList();

            UpdateStockAccountEntry updateStockAccountEntry = new(stockAccountEntry.AccountId, stockAccountEntry.EntryId,
                stockAccountEntry.PostingDate, stockAccountEntry.Value, stockAccountEntry.ValueChange, stockAccountEntry.Ticker,
                stockAccountEntry.InvestmentType, labels);

            await stockEntryHttpClient.UpdateEntryAsync(updateStockAccountEntry);
            return;
        }
        else if (accountEntry is BondAccountEntry bondAccountEntry)
        {
            UpdateBondAccountEntry updateBondAccountEntry = new(bondAccountEntry.AccountId, bondAccountEntry.EntryId,
                bondAccountEntry.PostingDate, bondAccountEntry.Value, bondAccountEntry.ValueChange, bondAccountEntry.BondDetailsId);
            await bondEntryHttpClient.UpdateEntryAsync(updateBondAccountEntry);
            return;
        }

        throw new NotSupportedException($"Account entry {accountEntry.GetType()} type not supported for getting start date.");
    }
}