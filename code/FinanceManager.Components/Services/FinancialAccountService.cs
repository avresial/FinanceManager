using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

public class FinancialAccountService : IFinancialAccountService
{
    private readonly BankAccountService _bankAccountService;
    private readonly StockAccountService _stockAccountService;
    private readonly ILogger<FinancialAccountService> logger;

    public FinancialAccountService(BankAccountService bankAccountService, StockAccountService stockAccountService, ILogger<FinancialAccountService> logger)
    {
        _bankAccountService = bankAccountService;
        _stockAccountService = stockAccountService;
        this.logger = logger;
    }

    public async Task<bool> AccountExists(int id)
    {
        if ((await _bankAccountService.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;
        if ((await _stockAccountService.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;

        return false;
    }
    public async Task<bool> AccountExists<T>(int id)
    {
        if (typeof(T) == typeof(BankAccount)) return (await _bankAccountService.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);

        if (typeof(T) == typeof(StockAccount)) return (await _stockAccountService.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);

        return false;
    }
    public async Task AddAccount<T>(T account) where T : BasicAccountInformation
    {
        if (account is BankAccount) await _bankAccountService.AddAccountAsync(new AddAccount(account.Name));
        if (account is StockAccount) await _stockAccountService.AddAccountAsync(new AddAccount(account.Name));
    }
    public async Task AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data)
        where AccountType : BasicAccountInformation
        where EntryType : FinancialEntryBase
    {
        if (typeof(AccountType) == typeof(BankAccount))
        {
            var bankAccountId = await _bankAccountService.AddAccountAsync(new AddAccount(accountName));
            foreach (var item in data)
            {
                if (item is BankAccountEntry bankEntry)
                {
                    await _bankAccountService.AddEntryAsync(new AddBankAccountEntry(bankEntry));
                }
            }
        }
        else if (typeof(AccountType) == typeof(StockAccount))
        {
            var bankAccountId = await _stockAccountService.AddAccountAsync(new AddAccount(accountName));
            foreach (var item in data)
            {
                if (item is StockAccountEntry stockEntry)
                {
                    await _stockAccountService.AddEntryAsync(new AddStockAccountEntry(stockEntry));
                }
            }
        }
    }
    public async Task AddEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        switch (accountEntry)
        {
            case BankAccountEntry bankAccountEntry:
                await _bankAccountService.AddEntryAsync(new AddBankAccountEntry(bankAccountEntry));
                break;

            case StockAccountEntry stockAccountEntry:
                if (!await _stockAccountService.AddEntryAsync(new AddStockAccountEntry(stockAccountEntry)))
                    throw new Exception("Adding entry failed");
                break;

        }
    }
    public async Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        if (typeof(T) == typeof(BankAccount))
            return await _bankAccountService.GetAccountWithEntriesAsync(id, dateStart, dateEnd) as T;
        else if (typeof(T) == typeof(StockAccount))
            return await _stockAccountService.GetAccountWithEntriesAsync(id, dateStart, dateEnd) as T;

        return null;
    }
    public async Task<IEnumerable<T>> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        List<T> result = [];
        IEnumerable<AvailableAccount> accounts = [];

        if (typeof(T) == typeof(BankAccount))
            accounts = await _bankAccountService.GetAvailableAccountsAsync();
        else if (typeof(T) == typeof(StockAccount))
            accounts = await _stockAccountService.GetAvailableAccountsAsync();

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

        foreach (var account in await _bankAccountService.GetAvailableAccountsAsync())
            result.Add(account.AccountId, typeof(BankAccount));

        foreach (var account in await _stockAccountService.GetAvailableAccountsAsync())
            result.Add(account.AccountId, typeof(StockAccount));

        return result;
    }
    public async Task<DateTime?> GetEndDate(int accountId)
    {
        if ((await _bankAccountService.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await _bankAccountService.GetYoungestEntryDate(accountId);

        if ((await _stockAccountService.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await _stockAccountService.GetYoungestEntryDate(accountId);

        return null;
    }
    public async Task<DateTime?> GetStartDate(int accountId)
    {
        if ((await _bankAccountService.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await _bankAccountService.GetOldestEntryDate(accountId);

        if ((await _stockAccountService.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await _stockAccountService.GetOldestEntryDate(accountId);
        return null;
    }
    public async Task<int?> GetLastAccountId()
    {
        List<AvailableAccount> accounts = (await _bankAccountService.GetAvailableAccountsAsync()).ToList();
        accounts.AddRange((await _stockAccountService.GetAvailableAccountsAsync()).ToList());

        if (accounts.Count == 0) return 0;

        return accounts.Max(x => x.AccountId);
    }

    public void InitializeMock()
    {
        logger.LogInformation("InitializeMock is called.");
    }
    public async Task RemoveAccount(int id)
    {
        if (await AccountExists<BankAccount>(id))
            await _bankAccountService.DeleteAccountAsync(new DeleteAccount(id));

        if (await AccountExists<StockAccount>(id))
            await _stockAccountService.DeleteAccountAsync(new DeleteAccount(id));
    }
    public async Task RemoveEntry(int entryId, int accountId)
    {
        if (await AccountExists<BankAccount>(accountId))
            await _bankAccountService.DeleteEntryAsync(accountId, entryId);

        if (await AccountExists<StockAccount>(accountId))
            await _stockAccountService.DeleteEntryAsync(accountId, entryId);
    }
    public async Task UpdateAccount<T>(T account) where T : BasicAccountInformation
    {
        if (account is BankAccount bankAccount)
            await _bankAccountService.UpdateAccountAsync(new UpdateAccount(bankAccount.AccountId, bankAccount.Name, bankAccount.AccountType));

        if (account is StockAccount)
            await _stockAccountService.UpdateAccountAsync(new UpdateAccount(account.AccountId, account.Name));
    }
    public async Task UpdateEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        if (accountEntry is BankAccountEntry bankAccountEntry)
            await _bankAccountService.UpdateEntryAsync(bankAccountEntry);
        else if (accountEntry is StockAccountEntry stockAccountEntry)
            await _stockAccountService.UpdateEntryAsync(stockAccountEntry);
    }
}
