using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
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

    public async Task<List<CurrencyAccountEntry>> GetEntries(int userId, List<RecurringTransactionEntryReference> entryReferences)
    {
        string endpoint = $"{httpClient.BaseAddress}api/RecurringTransactionDetector/Entries/{userId}";
        var response = await httpClient.PostAsJsonAsync(endpoint, entryReferences);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<CurrencyAccountEntry>>() ?? [];
    }
}
