using FinanceManager.Domain.Labels.Commands;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Labels.HttpClients;

public class RecurringTransactionDetectorHttpClient(HttpClient httpClient)
{
    public async Task<List<RecurringTransactionResult>> GetRecurringTransactions(
        int userId,
        CancellationToken cancellationToken = default)
    {
        string endpoint = $"{httpClient.BaseAddress}api/RecurringTransactionDetector/Get/{userId}";
        var result = await httpClient.GetFromJsonAsync<List<RecurringTransactionResult>>(endpoint, cancellationToken);
        return result ?? [];
    }

    public async Task<bool> Update(
        int userId,
        Guid patternId,
        UpdateRecurringSubscription command,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PutAsJsonAsync(
            $"api/RecurringTransactionDetector/{userId}/{patternId}",
            command,
            cancellationToken);
        return response.IsSuccessStatusCode;
    }
}