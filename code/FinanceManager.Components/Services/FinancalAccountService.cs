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

    public void AddAccount<T>(T account) where T : BasicAccountInformation
    {
        throw new NotImplementedException();
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

    public T? GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        throw new NotImplementedException();
    }

    public IEnumerable<T> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        throw new NotImplementedException();
    }

    public Dictionary<int, Type> GetAvailableAccounts()
    {
        throw new NotImplementedException();
    }

    public DateTime? GetEndDate(int id)
    {
        throw new NotImplementedException();
    }

    public int? GetLastAccountId()
    {
        throw new NotImplementedException();
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
