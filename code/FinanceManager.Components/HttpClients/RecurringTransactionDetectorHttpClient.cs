using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Labels.Entities;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class RecurringTransactionDetectorHttpClient(HttpClient httpClient)
{
    public async Task<List<RecurringTransactionResult>> GetRecurringTransactions(int userId)
    {
        string endpoint = $"{httpClient.BaseAddress}api/RecurringTransactionDetector/Get/{userId}";
        var result = await httpClient.GetFromJsonAsync<List<RecurringTransactionResult>>(endpoint);
        return result ?? [];
    }
}