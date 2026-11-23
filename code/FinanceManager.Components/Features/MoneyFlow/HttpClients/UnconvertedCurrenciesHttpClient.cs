using System.Net.Http.Json;

namespace FinanceManager.Components.Features.MoneyFlow.HttpClients;

public class UnconvertedCurrenciesHttpClient(HttpClient httpClient)
{
    // Several cards ask for the same answer in one paint; they share the request for a few seconds. An entry added later
    // is picked up by the next paint after the window.
    private static readonly TimeSpan _shareWindow = TimeSpan.FromSeconds(5);
    private (int UserId, int CurrencyId, DateTime FetchedAt, Task<IReadOnlyList<string>> Codes)? _last;

    public Task<IReadOnlyList<string>> GetCodes(int userId, int currencyId)
    {
        if (_last is { } last && last.UserId == userId && last.CurrencyId == currencyId
            && DateTime.UtcNow - last.FetchedAt < _shareWindow && !last.Codes.IsFaulted)
            return last.Codes;

        var codes = FetchAsync(userId, currencyId);
        _last = (userId, currencyId, DateTime.UtcNow, codes);
        return codes;
    }

    private async Task<IReadOnlyList<string>> FetchAsync(int userId, int currencyId) =>
        await httpClient.GetFromJsonAsync<List<string>>($"{httpClient.BaseAddress}api/UnconvertedCurrencies/{userId}/{currencyId}") ?? [];
}