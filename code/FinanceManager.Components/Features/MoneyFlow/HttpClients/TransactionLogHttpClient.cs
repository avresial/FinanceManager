using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Contracts.Dashboard;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using System.Globalization;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.MoneyFlow.HttpClients;

public class TransactionLogHttpClient(HttpClient httpClient)
{
    public async Task<List<TransactionLogEntryDto>> GetLastTransactions(int userId, int count)
    {
        string endpoint = $"{httpClient.BaseAddress}api/TransactionLog/Get/{userId}?count={count}";
        var result = await httpClient.GetFromJsonAsync<List<TransactionLogEntryDto>>(endpoint);
        return result ?? [];
    }

    public async Task<TransactionLogPage> GetPage(int userId, TransactionLogQuery query, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<TransactionLogPage>(BuildEndpoint("Page", userId, query), cancellationToken)
            ?? new TransactionLogPage([], null);

    /// <summary>The Transactions page feed: same selection and cursor as <see cref="GetPage"/>, typed by account kind.</summary>
    public async Task<TransactionHistoryPage> GetItemsPage(int userId, TransactionLogQuery query, CancellationToken cancellationToken = default)
    {
        var page = await httpClient.GetFromJsonAsync<TransactionLogItemsPageDto>(BuildEndpoint("ItemsPage", userId, query), cancellationToken);
        return page is null ? new TransactionHistoryPage([], null) : ToHistoryPage(userId, page);
    }

    internal static TransactionHistoryPage ToHistoryPage(int userId, TransactionLogItemsPageDto page)
    {
        // The rows and their editors only read the account's ID, name and (cash) currency, so one lightweight
        // account per ID is enough; it is shared by that account's rows on the page.
        Dictionary<int, CurrencyAccount> currencyAccounts = [];
        Dictionary<int, BondAccount> bondAccounts = [];
        List<TransactionHistoryItem> items = [];
        foreach (var item in page.Items)
        {
            items.Add(item switch
            {
                CurrencyTransactionLogItemDto currency => new CurrencyTransactionHistoryItem(
                    GetOrAdd(currencyAccounts, currency.AccountId, () => new CurrencyAccount(userId, currency.AccountId, currency.AccountName, AccountLabel.Other, currency.CurrencyId)),
                    currency.Currency, AccountEntryMapping.ToDomain(currency.Entry)),
                BondTransactionLogItemDto bond => new BondTransactionHistoryItem(
                    GetOrAdd(bondAccounts, bond.AccountId, () => new BondAccount(userId, bond.AccountId, bond.AccountName, AccountLabel.Other)),
                    AccountEntryMapping.ToDomain(bond.Entry)),
                InvestmentTransactionLogItemDto investment => new InvestmentTransactionHistoryItem(
                    investment.AccountId, investment.AccountName, investment.Transaction),
                _ => throw new NotSupportedException($"Transaction log item {item.GetType().Name} is not supported."),
            });
        }

        TransactionLogCursor? next = page.NextCursor is TransactionLogCursorDto cursor
            ? new(cursor.Date, (AccountType)cursor.AccountType, cursor.EntryId)
            : null;
        return new TransactionHistoryPage(items, next);
    }

    private static T GetOrAdd<T>(Dictionary<int, T> accounts, int accountId, Func<T> create)
    {
        if (!accounts.TryGetValue(accountId, out var account))
            accounts[accountId] = account = create();
        return account;
    }

    private static string BuildEndpoint(string route, int userId, TransactionLogQuery query)
    {
        var endpoint = $"api/TransactionLog/{route}/{userId}?start={FormatDate(query.Start)}&end={FormatDate(query.End)}&pageSize={query.PageSize.ToString(CultureInfo.InvariantCulture)}";
        if (query.Cursor is TransactionLogCursor cursor)
            endpoint += $"&cursorDate={FormatDate(cursor.Date)}&cursorAccountType={cursor.AccountType}&cursorEntryId={cursor.EntryId.ToString(CultureInfo.InvariantCulture)}";
        foreach (var type in query.AccountTypes ?? [])
            endpoint += $"&accountTypes={Uri.EscapeDataString(type.ToString())}";
        foreach (var accountId in query.AccountIds ?? [])
            endpoint += $"&accountIds={accountId.ToString(CultureInfo.InvariantCulture)}";
        if (!string.IsNullOrWhiteSpace(query.Search))
            endpoint += $"&search={Uri.EscapeDataString(query.Search)}";
        return endpoint;
    }

    private static string FormatDate(DateTime date) => Uri.EscapeDataString(date.ToString("O", CultureInfo.InvariantCulture));
}