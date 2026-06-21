using FinanceManager.Components.Components.Shared.Dialogs;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Assets.Dtos;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.Admin;

public partial class AdminEditAsset : ComponentBase
{
    [Parameter] public long? Id { get; set; }

    [Inject] public required AssetHttpClient AssetHttpClient { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }

    private bool _isLoading = true;
    private bool _isSaving;
    private bool _isNew;
    private AssetDto? _asset;
    private AssetListingDto? _newListing;
    private readonly Dictionary<long, MarketDataSymbolDto> _newSymbols = [];
    private readonly List<string> _errors = [];
    private readonly List<string> _info = [];

    private DateTime? InceptionDateTime
    {
        get => _asset?.InceptionDate is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : null;
        set { if (_asset is not null) _asset.InceptionDate = value is DateTime dt ? DateOnly.FromDateTime(dt) : null; }
    }

    protected override async Task OnInitializedAsync()
    {
        if (Id is null or 0)
        {
            _isNew = true;
            _asset = new AssetDto();
            _isLoading = false;
            return;
        }

        _asset = await AssetHttpClient.GetAsset(Id.Value);
        _isLoading = false;
    }

    private MarketDataSymbolDto NewSymbol(long listingId)
    {
        if (!_newSymbols.TryGetValue(listingId, out var dto))
        {
            dto = new MarketDataSymbolDto { IsEnabled = true };
            _newSymbols[listingId] = dto;
        }
        return dto;
    }

    private async Task SaveAsset()
    {
        if (_asset is null) return;
        _errors.Clear();
        _info.Clear();

        if (string.IsNullOrWhiteSpace(_asset.Name))
        {
            _errors.Add("Name is required.");
            return;
        }

        _isSaving = true;
        try
        {
            if (_isNew)
            {
                var created = await AssetHttpClient.CreateAsset(_asset);
                if (created is null)
                {
                    _errors.Add("Failed to create asset.");
                    return;
                }
                _asset = created;
                _isNew = false;
                _info.Add("Asset created. You can now add listings.");
            }
            else
            {
                if (await AssetHttpClient.UpdateAsset(_asset.Id, _asset))
                {
                    await ReloadAsset();
                    _info.Add("Asset saved.");
                }
                else
                {
                    _errors.Add("Failed to save asset.");
                }
            }
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to save asset: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void StartAddListing() =>
        _newListing = new AssetListingDto { PriceMultiplier = 1m, IsActive = true };

    private async Task CreateListing()
    {
        if (_asset is null || _newListing is null) return;
        _errors.Clear();
        _info.Clear();

        _isSaving = true;
        try
        {
            await AssetHttpClient.CreateListing(_asset.Id, _newListing);
            _newListing = null;
            await ReloadAsset();
            _info.Add("Listing added.");
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to add listing: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task SaveListing(AssetListingDto listing)
    {
        _errors.Clear();
        _info.Clear();
        _isSaving = true;
        try
        {
            if (await AssetHttpClient.UpdateListing(listing.Id, listing))
            {
                await ReloadAsset();
                _info.Add("Listing saved.");
            }
            else
            {
                _errors.Add("Failed to save listing.");
            }
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to save listing: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteListing(AssetListingDto listing)
    {
        var parameters = new DialogParameters<ConfirmDeleteDialog>
        {
            { x => x.Title, "Delete listing" },
            { x => x.Message, $"Delete listing {listing.Ticker} ({listing.ExchangeMic})?" }
        };
        var dialog = await DialogService.ShowAsync<ConfirmDeleteDialog>("Delete listing", parameters);
        var result = await dialog.Result;
        if (result is null || result.Canceled) return;

        _isSaving = true;
        try
        {
            if (await AssetHttpClient.DeleteListing(listing.Id))
                await ReloadAsset();
            else
                _errors.Add("Failed to delete listing.");
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to delete listing: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task AddSymbol(AssetListingDto listing)
    {
        var dto = NewSymbol(listing.Id);
        if (string.IsNullOrWhiteSpace(dto.Symbol))
        {
            _errors.Add("Symbol is required.");
            return;
        }

        _isSaving = true;
        try
        {
            await AssetHttpClient.CreateSymbol(listing.Id, dto);
            _newSymbols.Remove(listing.Id);
            await ReloadAsset();
            _info.Add("Symbol added.");
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to add symbol: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task SaveSymbol(MarketDataSymbolDto symbol)
    {
        _errors.Clear();
        _info.Clear();
        _isSaving = true;
        try
        {
            if (await AssetHttpClient.UpdateSymbol(symbol.Id, symbol))
                _info.Add("Symbol saved.");
            else
                _errors.Add("Failed to save symbol.");
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to save symbol: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task DeleteSymbol(MarketDataSymbolDto symbol)
    {
        _isSaving = true;
        try
        {
            if (await AssetHttpClient.DeleteSymbol(symbol.Id))
                await ReloadAsset();
            else
                _errors.Add("Failed to delete symbol.");
        }
        catch (Exception ex)
        {
            _errors.Add($"Failed to delete symbol: {ex.Message}");
        }
        finally
        {
            _isSaving = false;
        }
    }

    private async Task ReloadAsset()
    {
        if (_asset is null) return;
        var reloaded = await AssetHttpClient.GetAsset(_asset.Id);
        if (reloaded is not null) _asset = reloaded;
    }
}