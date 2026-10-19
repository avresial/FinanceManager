using FinanceManager.Domain.Dashboard.Dtos;
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

    public async Task<TransactionLogPage> GetPage(int userId, TransactionLogQuery query, CancellationToken cancellationToken = default)
    {
        var endpoint = $"api/TransactionLog/Page/{userId}?start={FormatDate(query.Start)}&end={FormatDate(query.End)}&pageSize={query.PageSize.ToString(CultureInfo.InvariantCulture)}";
        if (query.Cursor is TransactionLogCursor cursor)
            endpoint += $"&cursorDate={FormatDate(cursor.Date)}&cursorAccountType={cursor.AccountType}&cursorEntryId={cursor.EntryId.ToString(CultureInfo.InvariantCulture)}";
        return await httpClient.GetFromJsonAsync<TransactionLogPage>(endpoint, cancellationToken)
            ?? new TransactionLogPage([], null);
    }

    private static string FormatDate(DateTime date) => Uri.EscapeDataString(date.ToString("O", CultureInfo.InvariantCulture));
}