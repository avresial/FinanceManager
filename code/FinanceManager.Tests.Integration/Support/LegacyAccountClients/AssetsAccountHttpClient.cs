using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using System.Net;
using System.Net.Http.Json;

namespace FinanceManager.Tests.Integration.Support.LegacyAccountClients;

/// <summary>Reads the account-scoped gain/loss route that production no longer calls; it stays on the server until the account APIs are retired.</summary>
internal sealed class AssetsAccountHttpClient(HttpClient httpClient)
{
    public async Task<UnrealizedGainLossAccountResult?> GetUnrealizedGainLossForAccount(
        int userId, int accountId, Currency currency, DateTime asOfDate, CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.GetAsync(
            $"{httpClient.BaseAddress}api/Assets/GetUnrealizedGainLossForAccount/{userId}/{accountId}/{currency.Id}/{asOfDate:O}",
            cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound) return null;

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UnrealizedGainLossAccountResult>(cancellationToken: cancellationToken);
    }
}