using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Bond.Repositories;
using FinanceManager.Domain.FinancialAccounts.Currencies.Commands;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Repositories;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;

namespace FinanceManager.Infrastructure.Repositories;

public class AccountRepository(ICurrencyAccountRepository<CurrencyAccount> currencyAccountRepository, IAccountRepository<InvestmentAccount> investmentAccountRepository, IAccountRepository<BondAccount> bondAccountRepository,
    IAccountEntryRepository<CurrencyAccountEntry> currencyEntryRepository, IBondAccountEntryRepository<BondAccountEntry> bondEntryRepository
     ) : IFinancialAccountRepository
{
    private readonly Dictionary<(Type Type, int UserId, DateTime Start, DateTime End), IReadOnlyList<BasicAccountInformation>> _readCache = [];

    public async Task<Dictionary<int, Type>> GetAvailableAccounts(int userId)
    {
        Dictionary<int, Type> result = [];

        await foreach (var currencyAccount in currencyAccountRepository.GetAvailableAccounts(userId))
            result.Add(currencyAccount.AccountId, typeof(CurrencyAccount));

        await foreach (var investmentAccount in investmentAccountRepository.GetAvailableAccounts(userId))
            result.Add(investmentAccount.AccountId, typeof(InvestmentAccount));

        await foreach (var bondAccount in bondAccountRepository.GetAvailableAccounts(userId))
            result.Add(bondAccount.AccountId, typeof(BondAccount));

        return result;
    }
    public async Task<int> GetAccountsCount()
    {
        int currencyAccountsCount = await currencyAccountRepository.GetAccountsCount();
        int investmentAccountsCount = await investmentAccountRepository.GetAccountsCount();
        int bondAccountsCount = await bondAccountRepository.GetAccountsCount();

        return currencyAccountsCount + investmentAccountsCount + bondAccountsCount;
    }
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

            case Type t when t == typeof(InvestmentAccount):

                if (!await investmentAccountRepository.GetAvailableAccounts(userId).AnyAsync(x => x.AccountId == accountId))
                    throw new Exception($"User {userId} does not have account {accountId}");

                var investmentAccount = await investmentAccountRepository.Get(accountId) ?? throw new ArgumentNullException();

                if (investmentAccount is T investmentResult) return investmentResult;
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
        var cacheKey = (typeof(T), userId, dateStart, dateEnd);
        if (_readCache.TryGetValue(cacheKey, out var cachedAccounts))
        {
            foreach (var account in cachedAccounts)
                yield return (T)account;
            yield break;
        }

        List<T> accounts;
        switch (typeof(T))
        {
            case Type t when t == typeof(CurrencyAccount):
                accounts = (await GetCurrencyAccounts(userId, dateStart, dateEnd))
                    .Select(account => (T)(BasicAccountInformation)account)
                    .ToList();
                break;

            case Type t when t == typeof(InvestmentAccount):
                accounts = (await investmentAccountRepository.GetAll(userId))
                    .Select(account => (T)(BasicAccountInformation)account)
                    .ToList();
                break;

            case Type t when t == typeof(BondAccount):
                accounts = (await GetBondAccounts(userId, dateStart, dateEnd))
                    .Select(account => (T)(BasicAccountInformation)account)
                    .ToList();
                break;

            default:
                throw new NotSupportedException($"Account type {typeof(T)} is not supported.");
        }

        _readCache[cacheKey] = accounts;

        foreach (var account in accounts)
            yield return account;
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
        _readCache.Clear();
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

            case InvestmentAccount investmentAccount:
                return await investmentAccountRepository.Add(investmentAccount.UserId, investmentAccount.Name);

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
    public Task UpdateAccount<T>(T account) where T : BasicAccountInformation
    {
        _readCache.Clear();
        if (account is CurrencyAccount currencyAccount) return currencyAccountRepository.Update(currencyAccount.AccountId, currencyAccount.Name, currencyAccount.AccountType);
        if (account is InvestmentAccount investmentAccount) return investmentAccountRepository.Update(investmentAccount.AccountId, investmentAccount.Name);
        if (account is BondAccount bondAccount) return bondAccountRepository.Update(bondAccount.AccountId, bondAccount.Name);

        throw new NotSupportedException($"Account type {account.GetType()} is not supported.");
    }
    public async Task RemoveAccount(Type accountType, int id)
    {
        _readCache.Clear();
        switch (accountType)
        {
            case Type t when t == typeof(CurrencyAccount):
                if (await currencyAccountRepository.Exists(id))
                {
                    await currencyEntryRepository.Delete(id);
                    await currencyAccountRepository.Delete(id);
                }
                break;

            case Type t when t == typeof(InvestmentAccount):
                if (await investmentAccountRepository.Exists(id))
                    await investmentAccountRepository.Delete(id);
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

}