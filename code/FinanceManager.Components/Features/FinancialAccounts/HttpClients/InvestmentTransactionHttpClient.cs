using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FinanceManager.Components.Features.FinancialAccounts.HttpClients;

public class InvestmentTransactionHttpClient(HttpClient httpClient)
{
    /// <summary>
    /// Trades on the account, newest first. An empty list means the account genuinely has no trades:
    /// a failed request throws instead, so the snapshot-backed details page can keep the trades it
    /// already painted rather than persisting "this account is empty". See docs/codebase/UI-SNAPSHOTS.md.
    /// </summary>
    public async Task<IReadOnlyList<InvestmentTransactionDto>> GetByAccountAsync(int accountId)
    {
        using var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/InvestmentTransaction/GetByAccount/{accountId}");
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.NoContent) return [];
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<InvestmentTransactionDto>>() ?? [];
    }

    public async Task<InvestmentTransactionDto?> GetAsync(long id)
    {
        using var response = await httpClient.GetAsync($"{httpClient.BaseAddress}api/InvestmentTransaction/Get/{id}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<InvestmentTransactionDto>();
    }

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

    public async Task<IReadOnlyList<InstrumentSearchResultDto>> SearchListingsAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query)) return [];
        var url = $"{httpClient.BaseAddress}api/InvestmentTransaction/SearchListings?q={Uri.EscapeDataString(query)}&maxResults={maxResults}";
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
        var url = $"{httpClient.BaseAddress}api/InvestmentTransaction/SearchInstruments?q={Uri.EscapeDataString(query)}&maxResults={maxResults}";
        using var response = await httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode) return [];
        return await response.Content.ReadFromJsonAsync<List<InvestmentInstrumentOptionDto>>(cancellationToken) ?? [];
    }

    public async Task<InstrumentSearchResultDto?> GetListingAsync(long listingId)
    {
        using var response = await httpClient.GetAsync(
            $"{httpClient.BaseAddress}api/InvestmentTransaction/Listing/{listingId}");
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<InstrumentSearchResultDto>();
    }

    public async Task<ListingPriceDto?> GetListingPriceAsync(long listingId, DateOnly? asOf = null)
    {
        var url = $"{httpClient.BaseAddress}api/InvestmentTransaction/ListingPrice/{listingId}";
        if (asOf is DateOnly date)
            url += $"?asOf={date:yyyy-MM-dd}";

        using var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<ListingPriceDto>();
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