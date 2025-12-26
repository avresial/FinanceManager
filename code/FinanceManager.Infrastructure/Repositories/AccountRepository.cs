using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Cash;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories;

public class AccountRepository(IBankAccountRepository<BankAccount> bankAccountRepository, IAccountRepository<StockAccount> stockAccountRepository, IAccountRepository<BondAccount> bondAccountRepository,
    IStockAccountEntryRepository<StockAccountEntry> stockEntryRepository,
    IAccountEntryRepository<BankAccountEntry> bankEntryRepository, IBondAccountEntryRepository<BondAccountEntry> bondEntryRepository
     ) : IFinancialAccountRepository
{
    public async Task<Dictionary<int, Type>> GetAvailableAccounts(int userId)
    {
        Dictionary<int, Type> result = [];

        await foreach (var bankAccount in bankAccountRepository.GetAvailableAccounts(userId))
            result.Add(bankAccount.AccountId, typeof(BankAccount));

        await foreach (var stockAccount in stockAccountRepository.GetAvailableAccounts(userId))
            result.Add(stockAccount.AccountId, typeof(StockAccount));

        await foreach (var bondAccount in bondAccountRepository.GetAvailableAccounts(userId))
            result.Add(bondAccount.AccountId, typeof(BondAccount));

        return result;
    }
    public Task<int> GetLastAccountId() => throw new NotImplementedException();
    public async Task<int> GetAccountsCount()
    {
        int bankAccountsCount = await bankAccountRepository.GetAccountsCount();
        int stockAccountsCount = await stockAccountRepository.GetAccountsCount();
        int bondAccountsCount = await bondAccountRepository.GetAccountsCount();

        return bankAccountsCount + stockAccountsCount + bondAccountsCount;
    }
    public async Task<DateTime?> GetStartDate(int id)
    {
        var account = await FindAccount(id);
        if (account is null) return null;

        return account switch
        {
            BankAccount bankAccount => bankAccount.Start,
            StockAccount investmentAccount => investmentAccount.Start,
            BondAccount bondAccount => bondAccount.Start,
            _ => throw new NotSupportedException($"Account type {account.GetType()} is not supported."),
        };
    }
    public async Task<DateTime?> GetEndDate(int id)
    {
        var account = await FindAccount(id);
        if (account is null) return null;

        return account switch
        {
            BankAccount bankAccount => bankAccount.End,
            StockAccount investmentAccount => investmentAccount.End,
            BondAccount bondAccount => bondAccount.End,
            _ => throw new NotSupportedException($"Account type {account.GetType()} is not supported."),
        };
    }

    public Task<bool> AccountExists(int id) => throw new NotImplementedException();

    public async Task<T?> GetAccount<T>(int userId, int accountId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        switch (typeof(T))
        {
            case Type t when t == typeof(BankAccount):
                var resultAccount = await bankAccountRepository.Get(accountId);
                if (resultAccount is null) return null;
                if (resultAccount.UserId != userId) throw new Exception($"User {userId} does not have account {accountId}");
                var entries = await bankEntryRepository.Get(resultAccount.AccountId, dateStart, dateEnd).ToListAsync();

                var nextOlderEntry = await bankEntryRepository.GetNextOlder(resultAccount.AccountId, dateStart);
                var nextYoungerEntry = await bankEntryRepository.GetNextOlder(resultAccount.AccountId, dateStart);

                if (entries.Count == 0 && nextOlderEntry is not null)
                    entries = [nextOlderEntry];

                BankAccount newResultAccount = new(resultAccount.UserId, resultAccount.AccountId, resultAccount.Name, entries,
                    resultAccount.AccountType, nextOlderEntry, nextYoungerEntry);

                if (newResultAccount is T resultElement) return resultElement;

                break;

            case Type t when t == typeof(StockAccount):

                if (!await stockAccountRepository.GetAvailableAccounts(userId).AnyAsync(x => x.AccountId == accountId))
                    throw new Exception($"User {userId} does not have account {accountId}");

                var stockAccount = await stockAccountRepository.Get(accountId) ?? throw new ArgumentNullException();
                var stockEntries = await stockEntryRepository.Get(stockAccount.AccountId, dateStart, dateEnd).ToListAsync();
                var stockNextOlderEntry = await stockEntryRepository.GetNextOlder(stockAccount.AccountId, dateStart);
                var stockNextYoungerEntry = await stockEntryRepository.GetNextYounger(stockAccount.AccountId, dateStart);

                if (stockEntries.Count == 0 && stockNextOlderEntry is not null)
                    stockEntries = stockNextOlderEntry.Values.ToList();

                StockAccount newStockResultAccount = new(stockAccount.UserId, stockAccount.AccountId, stockAccount.Name, stockEntries, stockNextOlderEntry, stockNextYoungerEntry);

                if (newStockResultAccount is T newStockResult) return newStockResult;
                break;

            case Type t when t == typeof(BondAccount):

                if (!await bondAccountRepository.GetAvailableAccounts(userId).AnyAsync(x => x.AccountId == accountId))
                    throw new Exception($"User {userId} does not have account {accountId}");

                var bondAccount = await bondAccountRepository.Get(accountId) ?? throw new ArgumentNullException();
                var bondEntries = await bondEntryRepository.Get(bondAccount.AccountId, dateStart, dateEnd).ToListAsync();
                var bondNextOlderEntry = await bondEntryRepository.GetNextOlder(bondAccount.AccountId, dateStart);
                var bondNextYoungerEntry = await bondEntryRepository.GetNextYounger(bondAccount.AccountId, dateStart);

                if (bondEntries.Count == 0 && bondNextOlderEntry is not null)
                    bondEntries = bondNextOlderEntry.Values.ToList();

                BondAccount newBondResultAccount = new(bondAccount.UserId, bondAccount.AccountId, bondAccount.Name, bondEntries, AccountLabel.Bond, bondNextOlderEntry, bondNextYoungerEntry);

                if (newBondResultAccount is T newBondResult) return newBondResult;
                break;
        }

        throw new NotSupportedException($"Account type {typeof(T)} is not supported.");
    }
    public Task<T?> GetAccount<T>(int userId, int id) where T : BasicAccountInformation => GetAccount<T>(userId, id, DateTime.UtcNow, DateTime.UtcNow);
    public async IAsyncEnumerable<T> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        switch (typeof(T))
        {
            case Type t when t == typeof(BankAccount):
                foreach (var bankAccount in await bankAccountRepository.GetAvailableAccounts(userId).ToListAsync())
                    yield return (await GetAccount<T>(userId, bankAccount.AccountId, dateStart, dateEnd))!;
                yield break;

            case Type t when t == typeof(StockAccount):
                foreach (var stockAccount in await stockAccountRepository.GetAvailableAccounts(userId).ToListAsync())
                    yield return (await GetAccount<T>(userId, stockAccount.AccountId, dateStart, dateEnd))!;
                yield break;
            case Type t when t == typeof(BondAccount):
                foreach (var bondAccount in await bondAccountRepository.GetAvailableAccounts(userId).ToListAsync())
                    yield return (await GetAccount<T>(userId, bondAccount.AccountId, dateStart, dateEnd))!;
                yield break;
        }

        throw new NotSupportedException($"Account type {typeof(T)} is not supported.");
    }

    public async Task<int?> AddAccount<T>(T account) where T : BasicAccountInformation
    {
        switch (account)
        {
            case BankAccount bankAccount:
                var bankAccountId = await bankAccountRepository.Add(bankAccount.UserId, bankAccount.Name, bankAccount.AccountType);

                if (bankAccount is not null && bankAccount.Entries is not null)
                    await bankEntryRepository.Add(bankAccount.Entries.Select(x =>
                    {
                        x.AccountId = bankAccountId ?? 0;
                        return x;
                    }));

                return bankAccountId;

            case StockAccount stockAccount:

                var stockAccountId = await stockAccountRepository.Add(account.UserId, account.Name);

                if (stockAccount is not null && stockAccount.Entries is not null)
                    await stockEntryRepository.Add(stockAccount.Entries.Select(x =>
                    {
                        x.AccountId = stockAccountId ?? 0;
                        return x;
                    }));

                return stockAccountId;

            case BondAccount bondAccount:

                var bondAccountId = await bondAccountRepository.Add(account.UserId, account.Name);

                if (bondAccount is not null && bondAccount.Entries is not null)
                    await bondEntryRepository.Add(bondAccount.Entries.Select(x =>
                    {
                        x.AccountId = bondAccountId ?? 0;
                        return x;
                    }));

                return bondAccountId;

        }

        throw new NotSupportedException($"Account type {account.GetType()} is not supported.");
    }
    public Task AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data)
        where AccountType : BasicAccountInformation
        where EntryType : FinancialEntryBase => throw new NotImplementedException();
    public Task UpdateAccount<T>(T account) where T : BasicAccountInformation
    {
        if (account is BankAccount bankAccount) return bankAccountRepository.Update(bankAccount.AccountId, bankAccount.Name, bankAccount.AccountType);
        if (account is StockAccount stockAccount) return stockAccountRepository.Update(stockAccount.AccountId, stockAccount.Name);
        if (account is BondAccount bondAccount) return bondAccountRepository.Update(bondAccount.AccountId, bondAccount.Name);

        throw new NotSupportedException($"Account type {account.GetType()} is not supported.");
    }
    public async Task RemoveAccount(Type accountType, int id)
    {
        switch (accountType)
        {
            case Type t when t == typeof(BankAccount):
                if (await bankAccountRepository.Exists(id))
                {
                    await bankEntryRepository.Delete(id);
                    await bankAccountRepository.Delete(id);
                }
                break;

            case Type t when t == typeof(StockAccount):
                if (await stockAccountRepository.Exists(id))
                {
                    await stockEntryRepository.Delete(id);
                    await stockAccountRepository.Delete(id);
                }
                break;

            case Type t when t == typeof(BondAccount):
                if (await bondAccountRepository.Exists(id))
                {
                    await bondEntryRepository.Delete(id);
                    await bondAccountRepository.Delete(id);
                }
                break;
            default:
                throw new InvalidOperationException($"Account with id {id} not found.");
        }
    }

    public async Task<T?> GetNextYounger<T>(int accountId, DateTime date) where T : FinancialEntryBase => await bankEntryRepository.GetNextYounger(accountId, date) as T;
    public async Task AddEntry<T>(T accountEntry, int id) where T : FinancialEntryBase
    {
        if (accountEntry is BankAccountEntry bankEntry)
            await AddBankAccountEntry(id, bankEntry.ValueChange, bankEntry.Description, bankEntry.PostingDate);
        if (accountEntry is StockAccountEntry investmentEntry)
            await AddStockAccountEntry(id, investmentEntry.Ticker, investmentEntry.InvestmentType, investmentEntry.ValueChange, investmentEntry.PostingDate);

        throw new NotSupportedException($"Entry type {accountEntry.GetType()} is not supported.");
    }
    public Task<bool> AddLabel<T>(T accountEntry, int labelId) where T : FinancialEntryBase
    {
        if (accountEntry is BankAccountEntry bankEntry)
            return bankEntryRepository.AddLabel(bankEntry.EntryId, labelId);

        throw new NotSupportedException($"Entry type {accountEntry.GetType()} is not supported.");
    }
    public async Task UpdateEntry<T>(T accountEntry, int id) where T : FinancialEntryBase
    {
        if (accountEntry is BankAccountEntry bankEntry)
            await UpdateBankAccountEntry(id, bankEntry);
        else if (accountEntry is StockAccountEntry investmentEntry)
            await UpdateStockAccountEntry(id, investmentEntry);
        else
            throw new NotSupportedException($"Entry type {accountEntry.GetType()} is not supported.");
    }
    public async Task RemoveEntry(int accountEntryId, int id)
    {
        var account = await FindAccount(id);
        if (account is null) return;

        switch (account)
        {
            case BankAccount bankAccount:
                bankAccount.Remove(accountEntryId);
                break;
            case StockAccount investmentAccount:
                investmentAccount.Remove(accountEntryId);
                break;
        }
    }
    private Task<object?> FindAccount(int id) => throw new NotImplementedException();
    private Task<T?> FindAccount<T>(int id) where T : BasicAccountInformation => throw new NotImplementedException();


    private async Task AddStockAccountEntry(int id, string ticker, InvestmentType investmentType, decimal balanceChange, DateTime? postingDate = null)
    {
        var account = await FindAccount<StockAccount>(id);
        if (account is null) return;

        var finalPostingDate = postingDate ?? DateTime.UtcNow;

        account.Add(new(finalPostingDate, balanceChange, ticker, investmentType));
    }


    private async Task AddBankAccountEntry(int id, decimal balanceChange, string description, DateTime? postingDate = null)
    {
        var account = await FindAccount<BankAccount>(id);
        if (account is null) return;

        var finalPostingDate = postingDate ?? DateTime.UtcNow;

        account.AddEntry(new AddBankEntryDto(finalPostingDate, balanceChange, description, [new() { Name = "Sallary" }]));
    }
    private async Task UpdateBankAccountEntry(int id, BankAccountEntry bankAccountEntry)
    {
        var bankAccount = await FindAccount<BankAccount>(id);
        if (bankAccount is null || bankAccount.Entries is null) return;

        var entryToUpdate = bankAccount.Entries.FirstOrDefault(x => x.EntryId == bankAccountEntry.EntryId);
        if (entryToUpdate is null) return;

        entryToUpdate.Update(bankAccountEntry);
    }
    private async Task UpdateStockAccountEntry(int id, StockAccountEntry investmentEntry)
    {
        var investmentAccount = await FindAccount<StockAccount>(id);
        if (investmentAccount is null || investmentAccount.Entries is null) return;

        var entryToUpdate = investmentAccount.Entries.FirstOrDefault(x => x.EntryId == investmentEntry.EntryId);
        if (entryToUpdate is null) return;

        entryToUpdate.Update(investmentEntry);
    }
}