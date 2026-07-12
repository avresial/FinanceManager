using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using System.Net.Http.Json;

namespace FinanceManager.Components.HttpClients;

public class CurrencyHttpClient(HttpClient httpClient)
{
    public async Task<List<Currency>> GetAll() =>
        await httpClient.GetFromJsonAsync<List<Currency>>($"{httpClient.BaseAddress}api/Currency/GetAll") ?? [];
}