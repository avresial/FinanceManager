// Test-only copy of the Components client that called the legacy account API. The Components client was removed with the
// account screens (#1142); the server routes stay until #1139 deletes them, so their tests keep this client.
using FinanceManager.Contracts.FinancialAccounts.Investments;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceManager.Tests.Integration.Support.LegacyAccountClients;

public class InvestmentTransactionAccountClient(HttpClient httpClient)
{
    public async Task<InvestmentTransactionDto?> AddAsync(AddInvestmentTransactionRequest request)
    {
        using var response = await httpClient.PostAsJsonAsync($"{httpClient.BaseAddress}api/InvestmentTransaction/Add", request);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException(await GetProblemDetailAsync(response));
        return await response.Content.ReadFromJsonAsync<InvestmentTransactionDto>();
    }

    public async Task<bool> UpdateAsync(UpdateInvestmentTransactionRequest request)
    {
        using var response = await httpClient.PutAsJsonAsync($"{httpClient.BaseAddress}api/InvestmentTransaction/Update", request);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(int accountId, long id)
    {
        using var response = await httpClient.DeleteAsync($"{httpClient.BaseAddress}api/InvestmentTransaction/Delete/{accountId}/{id}");
        return response.IsSuccessStatusCode;
    }

    private static async Task<string> GetProblemDetailAsync(HttpResponseMessage response)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (problem.ValueKind == JsonValueKind.Object
                && problem.TryGetProperty("detail", out var detail)
                && detail.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(detail.GetString()))
                return detail.GetString()!;
        }
        catch (JsonException)
        {
        }

        return "Could not save the transaction.";
    }
}