using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Shared.Accounts;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Enums;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.Repositories.Account;

namespace FinanceManager.Infrastructure.Repositories;

public class AccountRepository(ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository, IAccountRepository<StockAccount> stockAccountRepository, IAccountRepository<BondAccount> bondAccountRepository,
    IStockAccountEntryRepository<StockAccountEntry> stockEntryRepository,
    IAccountEntryRepository<CurrencyAccountEntry> currencyEntryRepository, IBondAccountEntryRepository<BondAccountEntry> bondEntryRepository
     ) : IFinancialAccountRepository
{
    public async Task<Dictionary<int, Type>> GetAvailableAccounts(int userId)
    {
        Dictionary<int, Type> result = [];

        await foreach (var currencyAccount in currencyAccountRepository.GetAvailableAccounts(userId))
            result.Add(currencyAccount.AccountId, typeof(CurrencyAccount));

        await foreach (var stockAccount in stockAccountRepository.GetAvailableAccounts(userId))
            result.Add(stockAccount.AccountId, typeof(StockAccount));

        await foreach (var bondAccount in bondAccountRepository.GetAvailableAccounts(userId))
            result.Add(bondAccount.AccountId, typeof(BondAccount));

        return result;
    }
    public Task<int> GetLastAccountId() => throw new NotImplementedException();
    public async Task<int> GetAccountsCount()
    {
        int currencyAccountsCount = await currencyAccountRepository.GetAccountsCount();
        int stockAccountsCount = await stockAccountRepository.GetAccountsCount();
        int bondAccountsCount = await bondAccountRepository.GetAccountsCount();

        return currencyAccountsCount + stockAccountsCount + bondAccountsCount;
    }
    public async Task<DateTime?> GetStartDate(int id)
    {
        var account = await FindAccount(id);
        if (account is null) return null;

        return account switch
        {
            CurrencyAccount currencyAccount => currencyAccount.Start,
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
            CurrencyAccount currencyAccount => currencyAccount.End,
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
            case Type t when t == typeof(CurrencyAccount):
                var resultAccount = await currencyAccountRepository.Get(accountId);
                if (resultAccount is null) return null;
                if (resultAccount.UserId != userId) throw new Exception($"User {userId} does not have account {accountId}");
                var entries = await currencyEntryRepository.Get(resultAccount.AccountId, dateStart, dateEnd).ToListAsync();

                var nextOlderEntry = await currencyEntryRepository.GetNextOlder(resultAccount.AccountId, dateStart);
                var nextYoungerEntry = await currencyEntryRepository.GetNextYounger(resultAccount.AccountId, dateEnd);

                if (entries.Count == 0 && nextOlderEntry is not null)
                    entries = [nextOlderEntry];

                CurrencyAccount newResultAccount = new(resultAccount.UserId, resultAccount.AccountId, resultAccount.Name, entries,
                    resultAccount.AccountType, nextOlderEntry, nextYoungerEntry);

                if (newResultAccount is T resultElement) return resultElement;

                break;

            case Type t when t == typeof(StockAccount):

                if (!await stockAccountRepository.GetAvailableAccounts(userId).AnyAsync(x => x.AccountId == accountId))
                    throw new Exception($"User {userId} does not have account {accountId}");

                var stockAccount = await stockAccountRepository.Get(accountId) ?? throw new ArgumentNullException();
                var stockEntries = await stockEntryRepository.Get(stockAccount.AccountId, dateStart, dateEnd).ToListAsync();
                var stockNextOlderEntry = await stockEntryRepository.GetNextOlder(stockAccount.AccountId, dateStart);
                var stockNextYoungerEntry = await stockEntryRepository.GetNextYounger(stockAccount.AccountId, dateEnd);

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
                var bondNextYoungerEntry = await bondEntryRepository.GetNextYounger(bondAccount.AccountId, dateEnd);

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
            case Type t when t == typeof(CurrencyAccount):
                foreach (var account in await GetCurrencyAccounts(userId, dateStart, dateEnd))
                    yield return (T)(BasicAccountInformation)account;
                yield break;

            case Type t when t == typeof(StockAccount):
                foreach (var account in await GetStockAccounts(userId, dateStart, dateEnd))
                    yield return (T)(BasicAccountInformation)account;
                yield break;

            case Type t when t == typeof(BondAccount):
                foreach (var account in await GetBondAccounts(userId, dateStart, dateEnd))
                    yield return (T)(BasicAccountInformation)account;
                yield break;
        }

        throw new NotSupportedException($"Account type {typeof(T)} is not supported.");
    }

    // The methods below load a whole user's accounts of one type in a constant number of queries
    // (accounts, in-range entries, next-older boundary, next-younger boundary) instead of the ~5 queries
    // per account that GetAccount issues. This collapses the dashboard N+1 described in issue #411.
    private async Task<List<CurrencyAccount>> GetCurrencyAccounts(int userId, DateTime dateStart, DateTime dateEnd)
    {
        var accounts = await currencyAccountRepository.GetAll(userId);
        if (accounts.Count == 0) return [];

        var accountIds = accounts.Select(a => a.AccountId).ToList();
        var entriesByAccount = (await currencyEntryRepository.GetRange(accountIds, dateStart, dateEnd))
            .GroupBy(e => e.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var nextOlder = await currencyEntryRepository.GetNextOlder(accountIds, dateStart);
        var nextYounger = await currencyEntryRepository.GetNextYounger(accountIds, dateEnd);

        List<CurrencyAccount> result = [];
        foreach (var account in accounts)
        {
            List<CurrencyAccountEntry> entries = entriesByAccount.TryGetValue(account.AccountId, out var e) ? e : [];
            nextOlder.TryGetValue(account.AccountId, out var older);
            nextYounger.TryGetValue(account.AccountId, out var younger);

            if (entries.Count == 0 && older is not null)
                entries = [older];

            result.Add(new CurrencyAccount(account.UserId, account.AccountId, account.Name, entries, account.AccountType, older, younger));
        }

        return result;
    }

    private async Task<List<StockAccount>> GetStockAccounts(int userId, DateTime dateStart, DateTime dateEnd)
    {
        var accounts = await stockAccountRepository.GetAll(userId);
        if (accounts.Count == 0) return [];

        var accountIds = accounts.Select(a => a.AccountId).ToList();
        var entriesByAccount = (await stockEntryRepository.GetRange(accountIds, dateStart, dateEnd))
            .GroupBy(e => e.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var nextOlder = await stockEntryRepository.GetNextOlderPerInstrument(accountIds, dateStart);
        var nextYounger = await stockEntryRepository.GetNextYoungerPerInstrument(accountIds, dateEnd);

        List<StockAccount> result = [];
        foreach (var account in accounts)
        {
            List<StockAccountEntry> entries = entriesByAccount.TryGetValue(account.AccountId, out var e) ? e : [];
            var older = nextOlder.TryGetValue(account.AccountId, out var o) ? o : [];
            var younger = nextYounger.TryGetValue(account.AccountId, out var y) ? y : [];

            if (entries.Count == 0 && older.Count > 0)
                entries = older.Values.ToList();

            result.Add(new StockAccount(account.UserId, account.AccountId, account.Name, entries, older, younger));
        }

        return result;
    }

    private async Task<List<BondAccount>> GetBondAccounts(int userId, DateTime dateStart, DateTime dateEnd)
    {
        var accounts = await bondAccountRepository.GetAll(userId);
        if (accounts.Count == 0) return [];

        var accountIds = accounts.Select(a => a.AccountId).ToList();
        var entriesByAccount = (await bondEntryRepository.GetRange(accountIds, dateStart, dateEnd))
            .GroupBy(e => e.AccountId)
            .ToDictionary(g => g.Key, g => g.ToList());
        var nextOlder = await bondEntryRepository.GetNextOlderPerInstrument(accountIds, dateStart);
        var nextYounger = await bondEntryRepository.GetNextYoungerPerInstrument(accountIds, dateEnd);

        List<BondAccount> result = [];
        foreach (var account in accounts)
        {
            List<BondAccountEntry> entries = entriesByAccount.TryGetValue(account.AccountId, out var e) ? e : [];
            var older = nextOlder.TryGetValue(account.AccountId, out var o) ? o : [];
            var younger = nextYounger.TryGetValue(account.AccountId, out var y) ? y : [];

            if (entries.Count == 0 && older.Count > 0)
                entries = older.Values.ToList();

            result.Add(new BondAccount(account.UserId, account.AccountId, account.Name, entries, AccountLabel.Bond, older, younger));
        }

        return result;
    }

    public async Task<int?> AddAccount<T>(T account) where T : BasicAccountInformation
    {
        switch (account)
        {
            case CurrencyAccount currencyAccount:
                var currencyAccountId = await currencyAccountRepository.Add(currencyAccount.UserId, currencyAccount.Name, currencyAccount.AccountType);

                if (currencyAccount is not null && currencyAccount.Entries is not null)
                    await currencyEntryRepository.Add(currencyAccount.Entries.Select(x =>
                    {
                        x.AccountId = currencyAccountId ?? 0;
                        return x;
                    }));

                return currencyAccountId;

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
        if (account is CurrencyAccount currencyAccount) return currencyAccountRepository.Update(currencyAccount.AccountId, currencyAccount.Name, currencyAccount.AccountType);
        if (account is StockAccount stockAccount) return stockAccountRepository.Update(stockAccount.AccountId, stockAccount.Name);
        if (account is BondAccount bondAccount) return bondAccountRepository.Update(bondAccount.AccountId, bondAccount.Name);

        throw new NotSupportedException($"Account type {account.GetType()} is not supported.");
    }
    public async Task RemoveAccount(Type accountType, int id)
    {
        switch (accountType)
        {
            case Type t when t == typeof(CurrencyAccount):
                if (await currencyAccountRepository.Exists(id))
                {
                    await currencyEntryRepository.Delete(id);
                    await currencyAccountRepository.Delete(id);
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

    public async Task<T?> GetNextYounger<T>(int accountId, DateTime date) where T : FinancialEntryBase => await currencyEntryRepository.GetNextYounger(accountId, date) as T;
    public async Task AddEntry<T>(T accountEntry, int id) where T : FinancialEntryBase
    {
        if (accountEntry is CurrencyAccountEntry currencyEntry)
            await AddCurrencyAccountEntry(id, currencyEntry.ValueChange, currencyEntry.Description, currencyEntry.ContractorDetails, currencyEntry.PostingDate);
        if (accountEntry is StockAccountEntry investmentEntry)
            await AddStockAccountEntry(id, investmentEntry.Ticker, investmentEntry.InvestmentType, investmentEntry.ValueChange, investmentEntry.PostingDate);

        throw new NotSupportedException($"Entry type {accountEntry.GetType()} is not supported.");
    }
    public Task<bool> AddLabel<T>(T accountEntry, int labelId) where T : FinancialEntryBase
    {
        if (accountEntry is CurrencyAccountEntry currencyEntry)
            return currencyEntryRepository.AddLabel(currencyEntry.EntryId, labelId);

        throw new NotSupportedException($"Entry type {accountEntry.GetType()} is not supported.");
    }
    public async Task UpdateEntry<T>(T accountEntry, int id) where T : FinancialEntryBase
    {
        if (accountEntry is CurrencyAccountEntry currencyEntry)
            await UpdateCurrencyAccountEntry(id, currencyEntry);
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
            case CurrencyAccount currencyAccount:
                currencyAccount.Remove(accountEntryId);
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


    private async Task AddCurrencyAccountEntry(int id, decimal balanceChange, string description, string? contractorDetails, DateTime? postingDate = null)
    {
        var account = await FindAccount<CurrencyAccount>(id);
        if (account is null) return;

        var finalPostingDate = postingDate ?? DateTime.UtcNow;

        account.AddEntry(new AddCurrencyEntryDto(finalPostingDate, balanceChange, description, contractorDetails, [new() { Name = "Sallary" }]));
    }
    private async Task UpdateCurrencyAccountEntry(int id, CurrencyAccountEntry currencyAccountEntry)
    {
        var currencyAccount = await FindAccount<CurrencyAccount>(id);
        if (currencyAccount is null || currencyAccount.Entries is null) return;

        var entryToUpdate = currencyAccount.Entries.FirstOrDefault(x => x.EntryId == currencyAccountEntry.EntryId);
        if (entryToUpdate is null) return;

        entryToUpdate.Update(currencyAccountEntry);
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