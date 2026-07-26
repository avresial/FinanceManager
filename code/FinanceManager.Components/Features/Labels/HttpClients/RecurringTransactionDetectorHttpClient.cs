using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Labels.HttpClients;

public class RecurringTransactionDetectorHttpClient(HttpClient httpClient)
{
    public async Task<List<RecurringTransactionResult>> GetRecurringTransactions(int userId)
    {
        string endpoint = $"{httpClient.BaseAddress}api/RecurringTransactionDetector/Get/{userId}";
        var result = await httpClient.GetFromJsonAsync<List<RecurringTransactionResult>>(endpoint);
        return result ?? [];
    }
}