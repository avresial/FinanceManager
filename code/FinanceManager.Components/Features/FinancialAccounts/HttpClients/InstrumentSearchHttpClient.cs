using FinanceManager.Contracts.FinancialAccounts.Investments;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.FinancialAccounts.HttpClients;

public class InstrumentSearchHttpClient(HttpClient httpClient)
{
    public async Task<IReadOnlyList<InstrumentSearchResultDto>> SearchListingsAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var url = $"{httpClient.BaseAddress}api/investments/instruments/listings?q={Uri.EscapeDataString(query)}&maxResults={maxResults}";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<InstrumentSearchResultDto>>(cancellationToken) ?? [];
    }

    public async Task<IReadOnlyList<InvestmentInstrumentOptionDto>> SearchInstrumentsAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var url = $"{httpClient.BaseAddress}api/investments/instruments/options?q={Uri.EscapeDataString(query)}&maxResults={maxResults}";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<InvestmentInstrumentOptionDto>>(cancellationToken) ?? [];
    }

    public async Task<InstrumentSearchResultDto?> GetListingAsync(long listingId)
    {
        using var response = await httpClient.GetAsync(
            $"{httpClient.BaseAddress}api/investments/instruments/listings/{listingId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<InstrumentSearchResultDto>();
    }

    public async Task<ListingPriceDto?> GetListingPriceAsync(long listingId, DateOnly? asOf = null)
    {
        var url = $"{httpClient.BaseAddress}api/investments/instruments/listings/{listingId}/price";
        if (asOf is DateOnly date)
            url += $"?asOf={date:yyyy-MM-dd}";

        using var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ListingPriceDto>();
    }
}