using FinanceManager.Domain.Assets.Dtos;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace FinanceManager.Components.Features.Administration.HttpClients;

/// <summary>
/// Typed client for the admin asset-management API (assets, listings, provider symbols).
/// </summary>
public class AssetHttpClient(HttpClient httpClient, ILogger<AssetHttpClient> logger)
{
    private string Base => $"{httpClient.BaseAddress}api/admin/assets";

    public async Task<List<AssetDto>> GetAssets(CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await httpClient.GetFromJsonAsync<List<AssetDto>>(Base, cancellationToken);
            return result ?? [];
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching assets: {Message}", ex.Message);
            return [];
        }
    }

    public async Task<AssetDto?> GetAsset(long id, CancellationToken cancellationToken = default)
    {
        try
        {
            return await httpClient.GetFromJsonAsync<AssetDto>($"{Base}/{id}", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error fetching asset {Id}: {Message}", id, ex.Message);
            return null;
        }
    }

    public async Task<AssetDto?> CreateAsset(AssetDto asset, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync(Base, asset, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AssetDto>(cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateAsset(long id, AssetDto asset, CancellationToken cancellationToken = default) =>
        (await httpClient.PutAsJsonAsync($"{Base}/{id}", asset, cancellationToken)).IsSuccessStatusCode;

    public async Task<bool> DeleteAsset(long id, CancellationToken cancellationToken = default) =>
        (await httpClient.DeleteAsync($"{Base}/{id}", cancellationToken)).IsSuccessStatusCode;

    public async Task<AssetListingDto?> CreateListing(long assetId, AssetListingDto listing, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{Base}/{assetId}/listings", listing, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AssetListingDto>(cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateListing(long id, AssetListingDto listing, CancellationToken cancellationToken = default) =>
        (await httpClient.PutAsJsonAsync($"{Base}/listings/{id}", listing, cancellationToken)).IsSuccessStatusCode;

    public async Task<bool> DeleteListing(long id, CancellationToken cancellationToken = default) =>
        (await httpClient.DeleteAsync($"{Base}/listings/{id}", cancellationToken)).IsSuccessStatusCode;

    public async Task<MarketDataSymbolDto?> CreateSymbol(long listingId, MarketDataSymbolDto symbol, CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync($"{Base}/listings/{listingId}/symbols", symbol, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<MarketDataSymbolDto>(cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateSymbol(long id, MarketDataSymbolDto symbol, CancellationToken cancellationToken = default) =>
        (await httpClient.PutAsJsonAsync($"{Base}/symbols/{id}", symbol, cancellationToken)).IsSuccessStatusCode;

    public async Task<bool> DeleteSymbol(long id, CancellationToken cancellationToken = default) =>
        (await httpClient.DeleteAsync($"{Base}/symbols/{id}", cancellationToken)).IsSuccessStatusCode;

    public async Task<bool> AddManualPrice(long listingId, ManualPriceRequest request, CancellationToken cancellationToken = default) =>
        (await httpClient.PostAsJsonAsync($"{Base}/listings/{listingId}/prices/manual", request, cancellationToken)).IsSuccessStatusCode;
}