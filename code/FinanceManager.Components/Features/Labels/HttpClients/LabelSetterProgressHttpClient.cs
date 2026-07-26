using FinanceManager.Domain.FinancialAccounts.Shared.Dtos;
using FinanceManager.Domain.Labels.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Labels.HttpClients;

public class LabelSetterProgressHttpClient(HttpClient httpClient)
{
    public async Task<LabelSetterProgressSnapshot?> GetSnapshot(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<LabelSetterProgressSnapshot>(
            $"{httpClient.BaseAddress}api/LabelSetterProgress",
            cancellationToken);
}