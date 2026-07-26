using FinanceManager.Domain.Dashboard.Dtos;
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
}