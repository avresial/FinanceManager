using FinanceManager.Application.Commands.Account;
using FinanceManager.Domain.Entities.Accounts;
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

    public void AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data)
        where AccountType : BasicAccountInformation
        where EntryType : FinancialEntryBase
    {
        throw new NotImplementedException();
    }

    public void AddEntry<T>(T accountEntry, int id) where T : FinancialEntryBase
    {
        throw new NotImplementedException();
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

    public DateTime? GetEndDate(int id)
    {
        throw new NotImplementedException();
    }

    public async Task<int?> GetLastAccountId()
    {
        var bankAccounts = await _bankAccountService.GetAvailableAccountsAsync();

        if (!bankAccounts.Any()) return 0;

        return bankAccounts.Max(x => x.AccountId);
    }

    public DateTime? GetStartDate(int id)
    {
        throw new NotImplementedException();
    }

    public void InitializeMock()
    {
        logger.LogInformation("InitializeMock is called.");
    }

    public void RemoveAccount(int id)
    {
        throw new NotImplementedException();
    }

    public void RemoveEntry(int accountEntryId, int id)
    {
        throw new NotImplementedException();
    }

    public void UpdateAccount<T>(T account) where T : BasicAccountInformation
    {
        throw new NotImplementedException();
    }

    public void UpdateEntry<T>(T accountEntry, int id) where T : FinancialEntryBase
    {
        throw new NotImplementedException();
    }
}
