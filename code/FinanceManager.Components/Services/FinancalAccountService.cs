using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Services;

public class FinancalAccountService : IFinancalAccountService
{
    private readonly BankAccountService _bankAccountService;
    private readonly ILogger<FinancalAccountService> logger;

    public FinancalAccountService(BankAccountService bankAccountService, ILogger<FinancalAccountService> logger)
    {
        _bankAccountService = bankAccountService;
        this.logger = logger;
    }

    public async Task<bool> AccountExists(int id)
    {
        var accounts = await _bankAccountService.GetAvailableAccountsAsync();

        return accounts.Any(x => x.AccountId == id);
    }

    public async Task AddAccount<T>(T account) where T : BasicAccountInformation
    {
        if (typeof(T) == typeof(BankAccount))
            await _bankAccountService.AddAccountAsync(new AddAccount(account.Name));
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
    }

    public async Task AddEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        if (accountEntry is BankAccountEntry bankAccountEntry)
            await _bankAccountService.AddEntryAsync(new AddBankAccountEntry(bankAccountEntry));
    }

    public async Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        if (typeof(T) == typeof(BankAccount))
            return await _bankAccountService.GetAccountWithEntriesAsync(id, dateStart, dateEnd) as T;

        return null;
    }

    public async Task<IEnumerable<T>> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        List<T> result = [];
        var accounts = await _bankAccountService.GetAvailableAccountsAsync();
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
        var accounts = await _bankAccountService.GetAvailableAccountsAsync();
        return accounts.ToDictionary(x => x.AccountId, x => typeof(BankAccount));
    }

    public async Task<DateTime?> GetEndDate(int accountId)
    {
        return await _bankAccountService.GetYoungestEntryDate(accountId);
    }
    public async Task<DateTime?> GetStartDate(int accountId)
    {
        return await _bankAccountService.GetOldestEntryDate(accountId);
    }

    public async Task<int?> GetLastAccountId()
    {
        var bankAccounts = await _bankAccountService.GetAvailableAccountsAsync();

        if (!bankAccounts.Any()) return 0;

        return bankAccounts.Max(x => x.AccountId);
    }



    public void InitializeMock()
    {
        logger.LogInformation("InitializeMock is called.");
    }

    public async Task RemoveAccount(int id)
    {
        await _bankAccountService.DeleteAccountAsync(new DeleteAccount(id));
    }

    public async Task RemoveEntry(int accountEntryId, int entryId)
    {
        await _bankAccountService.DeleteEntryAsync(accountEntryId, entryId);
    }

    public async Task UpdateAccount<T>(T account) where T : BasicAccountInformation
    {
        await _bankAccountService.UpdateAccountAsync(new UpdateAccount(account.AccountId, account.Name));
    }

    public async Task UpdateEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        if (accountEntry is BankAccountEntry bankAccountEntry)
            await _bankAccountService.UpdateEntryAsync(bankAccountEntry);
    }
}
