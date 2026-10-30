using FinanceManager.Domain.Assets.Discovery;
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

    public async Task<IncompleteAssetPage> GetIncomplete(int page, string? search, CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<IncompleteAssetPage>($"{Base}/incomplete?page={page}&pageSize=20&search={Uri.EscapeDataString(search ?? string.Empty)}", ct)
        ?? throw new InvalidOperationException("The incomplete assets response was empty.");

    public async Task<MarketDataSymbolDto> ValidateQueueSymbol(long listingId, MarketDataSymbolDto symbol, CancellationToken ct = default)
    {
        using var response = await httpClient.PostAsJsonAsync($"{Base}/incomplete/listings/{listingId}/symbols", symbol, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException((await response.Content.ReadAsStringAsync(ct)).Trim('"'));
        return await response.Content.ReadFromJsonAsync<MarketDataSymbolDto>(cancellationToken: ct)
            ?? throw new InvalidOperationException("The symbol response was empty.");
    }

    public async Task<List<InstrumentDiscoveryResultDto>> SearchExternal(string query, CancellationToken ct = default) =>
        await httpClient.GetFromJsonAsync<List<InstrumentDiscoveryResultDto>>($"{Base}/discovery/search?query={Uri.EscapeDataString(query)}", ct) ?? [];

    public Task<AdminAssetDraftDto> GetExternalDraft(InstrumentDiscoveryResultDto instrument, CancellationToken ct = default) =>
        PostDiscovery<InstrumentDiscoveryResultDto, AdminAssetDraftDto>("draft", instrument, ct);

    public Task<List<InstrumentDiscoveryResultDto>> FindExternalListings(AssetDto asset, CancellationToken ct = default) =>
        PostDiscovery<AssetDto, List<InstrumentDiscoveryResultDto>>("listings", asset, ct);

    public Task<List<MarketDataSymbolResolution>> FindExternalSymbols(InstrumentDiscoveryResultDto instrument, long? listingId = null, CancellationToken ct = default) =>
        PostDiscovery<InstrumentDiscoveryResultDto, List<MarketDataSymbolResolution>>($"symbols{(listingId is > 0 ? $"?listingId={listingId}" : string.Empty)}", instrument, ct);

    public Task<AssetDto> CreateExternalAsset(AssetDto asset, CancellationToken ct = default) =>
        PostDiscovery<AssetDto, AssetDto>("create", asset, ct);

    public Task<AssetListingDto> AddExternalListing(long assetId, AssetListingDto listing, CancellationToken ct = default) =>
        PostDiscovery<AssetListingDto, AssetListingDto>($"{assetId}/listings", listing, ct);

    private async Task<TResponse> PostDiscovery<TRequest, TResponse>(string path, TRequest request, CancellationToken ct)
    {
        using var response = await httpClient.PostAsJsonAsync($"{Base}/discovery/{path}", request, ct);
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException((await response.Content.ReadAsStringAsync(ct)).Trim('"'));
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken: ct)
            ?? throw new InvalidOperationException("The external discovery response was empty.");
    }

    public async Task<List<AssetDto>> GetAssets(CancellationToken cancellationToken = default) =>
        await httpClient.GetFromJsonAsync<List<AssetDto>>(Base, cancellationToken)
        ?? throw new InvalidOperationException("The assets response was empty.");

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