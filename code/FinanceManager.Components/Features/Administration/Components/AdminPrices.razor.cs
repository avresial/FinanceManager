using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Domain.Assets.Dtos;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Features.Administration.Components;

public partial class AdminPrices : ComponentBase
{
    private readonly List<ListingOption> _listings = [];
    private MudForm? _form;
    private long? _listingId;
    private DateTime? _date = DateTime.Today;
    private decimal? _price;
    private bool _isLoading = true;
    private bool _isSaving;
    private string? _loadError;

    [Inject] public required AssetHttpClient AssetHttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }

    private string? SelectedCurrency => _listings.FirstOrDefault(x => x.Id == _listingId)?.Currency;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var assets = await AssetHttpClient.GetAssets();
            var details = await Task.WhenAll(assets.Select(asset => AssetHttpClient.GetAsset(asset.Id)));
            if (details.Any(detail => detail is null))
                throw new InvalidOperationException("An asset listing response was unavailable.");

            _listings.AddRange(details.OfType<AssetDto>().SelectMany(asset => asset.Listings.Select(listing => new ListingOption(
                listing.Id,
                $"{asset.Name} · {listing.Ticker} · {listing.ExchangeName} · {listing.TradingCurrency}",
                listing.TradingCurrency))));
        }
        catch (Exception)
        {
            _loadError = "Unable to load asset listings. Refresh the page to try again.";
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task Save()
    {
        await _form!.Validate();
        if (!_form.IsValid || _listingId is not long listingId || _date is not DateTime date || _price is not decimal price || price <= 0m)
            return;

        _isSaving = true;
        try
        {
            var saved = await AssetHttpClient.AddManualPrice(
                listingId,
                new ManualPriceRequest(price, new DateTimeOffset(DateTime.SpecifyKind(date.Date, DateTimeKind.Utc))));
            Snackbar.Add(saved ? "Manual price saved." : "Unable to save the price. Try again.", saved ? Severity.Success : Severity.Error);
            if (saved) _price = null;
        }
        catch (Exception)
        {
            Snackbar.Add("Unable to save the price. Try again.", Severity.Error);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private sealed record ListingOption(long Id, string Label, string Currency);
}