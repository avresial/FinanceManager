using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Bond.Commands;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Commands;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using FinanceManager.Domain.Identity.Services;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.FinancialAccounts.Services;

public class FinancialAccountService(CurrencyAccountHttpClient currencyAccountHttpClient, CurrencyEntryHttpClient currencyEntryHttpClient,
    InvestmentAccountHttpClient investmentAccountHttpClient,
    BondAccountHttpClient bondAccountHttpClient, BondEntryHttpClient bondEntryHttpClient,
    AccountDataSynchronizationService accountDataSynchronizationService, ILoginService loginService,
    ILogger<FinancialAccountService> logger) : IFinancialAccountService
{
    // The account-id → type map only changes when accounts are added/removed, yet today it is fetched
    // (three account-endpoint round-trips) on every navigation to an account page, purely to decide
    // which details component to render. Cache it so re-navigation is instant. The cache is invalidated
    // on the service's own account mutations, on the app-wide AccountsChanged signal (covers accounts
    // added straight through the HttpClients, e.g. AddAccount.razor.cs), and on login-state changes so
    // it never leaks across users (logout does not reload the app, so this scoped instance is reused).
    // The in-flight (or completed) fetch is cached rather than only its result, so callers that ask at the
    // same time - Home and the nav menu both do on startup - share one set of requests. #890
    private Task<IReadOnlyList<TypedAvailableAccount>>? _availableAccountsTask;
    private bool _invalidationHooksInstalled;

    private sealed record TypedAvailableAccount(int AccountId, string AccountName, Type AccountType);


    public async Task<bool> AccountExists(int id)
    {
        if ((await currencyAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;
        if ((await investmentAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;
        if ((await bondAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id)) return true;

        return false;
    }
    public async Task<bool> AccountExists<T>(int id)
    {
        if (typeof(T) == typeof(CurrencyAccount)) return (await currencyAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);
        if (typeof(T) == typeof(InvestmentAccount)) return (await investmentAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);
        if (typeof(T) == typeof(BondAccount)) return (await bondAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == id);

        return false;
    }
    public async Task AddAccount<T>(T account) where T : BasicAccountInformation
    {
        switch (account)
        {
            case CurrencyAccount currencyAccount:
                await currencyAccountHttpClient.AddAccountAsync(new AddAccount(account.Name, currencyAccount.CurrencyId));
                break;
            case InvestmentAccount:
                await investmentAccountHttpClient.AddAccountAsync(new AddAccount(account.Name));
                break;
            case BondAccount:
                await bondAccountHttpClient.AddAccountAsync(new AddAccount(account.Name));
                break;

            default: throw new NotSupportedException($"Account type {typeof(T)} not supported for adding account.");
        }

        InvalidateAvailableAccountsCache();
    }
    public async Task AddAccount<AccountType, EntryType>(string accountName, List<EntryType> data)
        where AccountType : BasicAccountInformation
        where EntryType : FinancialEntryBase
    {
        if (typeof(AccountType) == typeof(CurrencyAccount))
        {
            await currencyAccountHttpClient.AddAccountAsync(new(accountName));
            foreach (var item in data)
            {
                if (item is CurrencyAccountEntry currencyEntry)
                {
                    await currencyEntryHttpClient.AddEntryAsync(new(currencyEntry.AccountId, currencyEntry.EntryId, currencyEntry.PostingDate,
                        currencyEntry.Value, currencyEntry.ValueChange, currencyEntry.Description, currencyEntry.ContractorDetails));
                }
            }
        }
        else if (typeof(AccountType) == typeof(InvestmentAccount))
        {
            // Investment accounts hold no per-account entries; holdings are added later as transactions.
            await investmentAccountHttpClient.AddAccountAsync(new(accountName));
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

        InvalidateAvailableAccountsCache();
    }
    public async Task AddEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        switch (accountEntry)
        {
            case CurrencyAccountEntry currencyEntry:
                await currencyEntryHttpClient.AddEntryAsync(new(currencyEntry.AccountId, currencyEntry.EntryId, currencyEntry.PostingDate, currencyEntry.Value,
                    currencyEntry.ValueChange, currencyEntry.Description, currencyEntry.ContractorDetails));
                break;

            case BondAccountEntry bondEntry:
                await bondEntryHttpClient.AddEntryAsync(new(bondEntry.AccountId, bondEntry.EntryId, bondEntry.PostingDate,
                    bondEntry.Value, bondEntry.ValueChange, bondEntry.BondDetailsId));
                break;

            default: throw new NotSupportedException($"Account entry {accountEntry.GetType()} type not supported for adding entry.");

        }
    }
    public async Task<T?> GetAccount<T>(int userId, int id, DateTime dateStart, DateTime dateEnd, int minimumEntryCount = 0) where T : BasicAccountInformation
    {
        if (typeof(T) == typeof(CurrencyAccount))
            return await currencyAccountHttpClient.GetAccountWithEntriesAsync(id, dateStart, dateEnd, minimumEntryCount) as T;
        else if (typeof(T) == typeof(InvestmentAccount))
            return await investmentAccountHttpClient.GetAccountAsync(id) as T;
        else if (typeof(T) == typeof(BondAccount))
            return await bondAccountHttpClient.GetAccountWithEntriesAsync(id, dateStart, dateEnd, minimumEntryCount) as T;

        throw new NotSupportedException($"Account type {typeof(T)} not supported for getting start date.");

    }

    public async Task<T?> GetInitialTransactionHistory<T>(int userId, int id, DateTime dateStart, DateTime dateEnd,
        int minimumEntryCount = 100) where T : BasicAccountInformation
    {
        if (typeof(T) == typeof(CurrencyAccount))
            return await currencyAccountHttpClient.GetInitialTransactionHistoryAsync(id, dateStart, dateEnd, minimumEntryCount) as T;
        else if (typeof(T) == typeof(InvestmentAccount))
            return await investmentAccountHttpClient.GetAccountAsync(id) as T;
        else if (typeof(T) == typeof(BondAccount))
            return await bondAccountHttpClient.GetInitialTransactionHistoryAsync(id, dateStart, dateEnd, minimumEntryCount) as T;

        throw new NotSupportedException($"Account type {typeof(T)} not supported for getting initial transaction history.");
    }

    public async Task<IEnumerable<T>> GetAccounts<T>(int userId, DateTime dateStart, DateTime dateEnd) where T : BasicAccountInformation
    {
        List<T> result = [];
        IEnumerable<AvailableAccount> accounts = [];

        if (typeof(T) == typeof(CurrencyAccount))
            accounts = await currencyAccountHttpClient.GetAvailableAccountsAsync();
        else if (typeof(T) == typeof(InvestmentAccount))
            accounts = await investmentAccountHttpClient.GetAvailableAccountsAsync();
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
        // Always hand back a fresh copy: the cached list is shared across all callers, so returning a shared
        // map would let any caller that mutates the result silently corrupt every other caller's view.
        var accounts = await GetCachedAvailableAccounts();
        return accounts.ToDictionary(account => account.AccountId, account => account.AccountType);
    }

    public async Task<Dictionary<int, string>> GetAvailableAccountNames()
    {
        var accounts = await GetCachedAvailableAccounts();
        return accounts.ToDictionary(account => account.AccountId, account => account.AccountName);
    }

    private async Task<IReadOnlyList<TypedAvailableAccount>> GetCachedAvailableAccounts()
    {
        EnsureInvalidationHooksInstalled();

        if (_availableAccountsTask is not null)
            return await _availableAccountsTask;

        var fetch = FetchAvailableAccounts();
        _availableAccountsTask = fetch;
        var result = await fetch;

        // An empty list most likely means the endpoints failed (per-type errors are swallowed below) or
        // the user genuinely has no accounts yet; don't pin it so a transient failure - or the guest
        // mock-seeding flow in Home.razor - self-heals on the next lookup. The reference check keeps a
        // fetch started after an invalidation from being dropped by this older one.
        if (result.Count == 0 && ReferenceEquals(_availableAccountsTask, fetch))
            _availableAccountsTask = null;

        return result;
    }

    private async Task<IReadOnlyList<TypedAvailableAccount>> FetchAvailableAccounts()
    {
        var currencyAccountsTask = GetAvailableAccountsAsync(
            () => currencyAccountHttpClient.GetAvailableAccountsAsync(),
            "currency");
        var investmentAccountsTask = GetAvailableAccountsAsync(
            async () => await investmentAccountHttpClient.GetAvailableAccountsAsync(),
            "investment");
        var bondAccountsTask = GetAvailableAccountsAsync(
            () => bondAccountHttpClient.GetAvailableAccountsAsync(),
            "bond");

        await Task.WhenAll(currencyAccountsTask, investmentAccountsTask, bondAccountsTask);

        return
        [
            .. (await currencyAccountsTask).Select(account => new TypedAvailableAccount(account.AccountId, account.AccountName, typeof(CurrencyAccount))),
            .. (await investmentAccountsTask).Select(account => new TypedAvailableAccount(account.AccountId, account.AccountName, typeof(InvestmentAccount))),
            .. (await bondAccountsTask).Select(account => new TypedAvailableAccount(account.AccountId, account.AccountName, typeof(BondAccount))),
        ];
    }

    // Subscribed lazily (rather than in a constructor body) so the primary constructor can be kept.
    // Both source services are scoped and share this service's lifetime, so unsubscribing is unnecessary.
    private void EnsureInvalidationHooksInstalled()
    {
        if (_invalidationHooksInstalled) return;
        _invalidationHooksInstalled = true;

        accountDataSynchronizationService.AccountsChanged += InvalidateAvailableAccountsCache;
        loginService.LogginStateChanged += _ => InvalidateAvailableAccountsCache();
    }

    private void InvalidateAvailableAccountsCache() => _availableAccountsTask = null;

    private async Task<IEnumerable<AvailableAccount>> GetAvailableAccountsAsync(
        Func<Task<IEnumerable<AvailableAccount>>> getAccounts,
        string accountType)
    {
        try
        {
            return await getAccounts();
        }
        catch (Exception ex)
        {
            logger.LogError("Error while fetching {AccountType} accounts: {Message}", accountType, ex.Message);
            return [];
        }
    }
    public async Task<DateTime?> GetEndDate(int accountId)
    {
        if ((await currencyAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await currencyEntryHttpClient.GetYoungestEntryDate(accountId);

        if ((await investmentAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return null;

        if ((await bondAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await bondEntryHttpClient.GetYoungestEntryDate(accountId);

        throw new NotSupportedException($"Account {accountId} type not supported for getting start date.");
    }
    public async Task<DateTime?> GetStartDate(int accountId)
    {
        if ((await currencyAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await currencyEntryHttpClient.GetOldestEntryDate(accountId);

        if ((await investmentAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return null;

        if ((await bondAccountHttpClient.GetAvailableAccountsAsync()).Any(x => x.AccountId == accountId))
            return await bondEntryHttpClient.GetOldestEntryDate(accountId);

        throw new NotSupportedException($"Account {accountId} type not supported for getting start date.");
    }
    public async Task<int?> GetLastAccountId()
    {
        List<AvailableAccount> accounts = [.. (await currencyAccountHttpClient.GetAvailableAccountsAsync())];
        accounts.AddRange([.. (await investmentAccountHttpClient.GetAvailableAccountsAsync())]);
        accounts.AddRange([.. (await bondAccountHttpClient.GetAvailableAccountsAsync())]);

        if (accounts.Count == 0) return 0;

        return accounts.Max(x => x.AccountId);
    }

    public void InitializeMock() => logger.LogInformation("InitializeMock is called.");
    public async Task RemoveAccount(int id)
    {
        if (await AccountExists<CurrencyAccount>(id))
        {
            await currencyAccountHttpClient.DeleteAccountAsync(id);
            InvalidateAvailableAccountsCache();
            return;
        }

        if (await AccountExists<InvestmentAccount>(id))
        {
            await investmentAccountHttpClient.DeleteAccountAsync(id);
            InvalidateAvailableAccountsCache();
            return;
        }

        if (await AccountExists<BondAccount>(id))
        {
            await bondAccountHttpClient.DeleteAccountAsync(id);
            InvalidateAvailableAccountsCache();
            return;
        }

        throw new NotSupportedException($"Account {id} type not supported for getting start date.");
    }
    public async Task RemoveEntry(int entryId, int accountId)
    {
        if (await AccountExists<CurrencyAccount>(accountId))
            await currencyEntryHttpClient.DeleteEntryAsync(accountId, entryId);
        else if (await AccountExists<BondAccount>(accountId))
            await bondEntryHttpClient.DeleteEntryAsync(accountId, entryId);

        else throw new InvalidOperationException($"Account {accountId}, entryId {entryId} not found.");
    }
    public async Task UpdateAccount<T>(T account) where T : BasicAccountInformation
    {
        if (account is CurrencyAccount currencyAccount)
            await currencyAccountHttpClient.UpdateAccountAsync(new(currencyAccount.AccountId, currencyAccount.Name, currencyAccount.AccountType, currencyAccount.CurrencyId));
        else if (account is InvestmentAccount)
            await investmentAccountHttpClient.UpdateAccountAsync(new(account.AccountId, account.Name, Domain.FinancialAccounts.Shared.Entities.AccountLabel.Stock));
        else if (account is BondAccount)
            await bondAccountHttpClient.UpdateAccountAsync(new(account.AccountId, account.Name, Domain.FinancialAccounts.Shared.Entities.AccountLabel.Bond));
        else
            throw new NotSupportedException($"Account {account.GetType()} type not supported for getting start date.");

        // The cached list carries account names (the nav menu reads them), so a rename must refetch it.
        InvalidateAvailableAccountsCache();
    }
    public async Task UpdateEntry<T>(T accountEntry) where T : FinancialEntryBase
    {
        if (accountEntry is CurrencyAccountEntry currencyAccountEntry)
        {
            await currencyEntryHttpClient.UpdateEntryAsync(currencyAccountEntry);
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