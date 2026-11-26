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

    public async Task<List<string>> GetCurrencies(int userId, CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<string>>($"api/TransactionLog/Currencies/{userId}", cancellationToken) ?? [];

    public async Task<TransactionHistoryPage> GetItemsPage(int userId, TransactionLogQuery query, CancellationToken cancellationToken = default)
    {
        var page = await httpClient.GetFromJsonAsync<TransactionLogItemsPageDto>(BuildEndpoint(userId, query), cancellationToken);
        return page is null ? new TransactionHistoryPage([], null) : ToHistoryPage(page);
    }

    internal static TransactionHistoryPage ToHistoryPage(TransactionLogItemsPageDto page)
    {
        var items = page.Items.Select<TransactionLogItemDto, TransactionHistoryItem>(item => item switch
        {
            CurrencyTransactionLogItemDto e => new CurrencyTransactionHistoryItem(
                e.Currency ?? string.Empty, new(e.LegacyAccountId, e.EntryId, e.PostingDate, 0, e.ValueChange)
                {
                    Description = e.Description,
                    ContractorDetails = e.ContractorDetails,
                    CurrencyId = e.CurrencyId,
                    Labels = [.. e.Labels.Select(FinancialLabelMapping.ToDomain)]
                }),
            BondTransactionLogItemDto e => new BondTransactionHistoryItem(
                new(e.LegacyAccountId, e.EntryId, e.PostingDate, 0, e.ValueChange, e.BondDetailsId)
                {
                    Description = e.Description,
                    Labels = [.. e.Labels.Select(FinancialLabelMapping.ToDomain)]
                }, e.BondName),
            InvestmentTransactionLogItemDto investment => new InvestmentTransactionHistoryItem(investment.Transaction),
            _ => throw new NotSupportedException($"Transaction log item {item.GetType().Name} is not supported.")
        }).ToList();

        TransactionLogCursor? next = page.NextCursor is TransactionLogCursorDto cursor
            ? new(cursor.Date, (TransactionKind)cursor.Kind, cursor.EntryId)
            : null;
        return new TransactionHistoryPage(items, next);
    }

    private static string BuildEndpoint(int userId, TransactionLogQuery query)
    {
        var endpoint = $"api/TransactionLog/ItemsPage/{userId}?start={FormatDate(query.Start)}&end={FormatDate(query.End)}&pageSize={query.PageSize.ToString(CultureInfo.InvariantCulture)}";
        if (query.Cursor is TransactionLogCursor cursor)
            endpoint += $"&cursorDate={FormatDate(cursor.Date)}&cursorKind={cursor.Kind}&cursorEntryId={cursor.EntryId.ToString(CultureInfo.InvariantCulture)}";
        foreach (var type in query.Kinds ?? [])
            endpoint += $"&kinds={Uri.EscapeDataString(type.ToString())}";
        foreach (var currency in query.Currencies ?? [])
            endpoint += $"&currencies={Uri.EscapeDataString(currency)}";
        if (!string.IsNullOrWhiteSpace(query.Search))
            endpoint += $"&search={Uri.EscapeDataString(query.Search)}";
        return endpoint;
    }

    private static string FormatDate(DateTime date) => Uri.EscapeDataString(date.ToString("O", CultureInfo.InvariantCulture));
}