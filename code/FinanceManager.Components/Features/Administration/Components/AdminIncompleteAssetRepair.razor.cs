using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.Assets.Entities;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Administration.Components;

public partial class AdminIncompleteAssetRepair : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required IncompleteAssetItem Item { get; set; }
    [Parameter] public EventCallback Saved { get; set; }
    [Parameter] public EventCallback Close { get; set; }
    [Inject] public required AssetHttpClient Client { get; set; }
    [Inject] public required CurrencyHttpClient CurrencyClient { get; set; }
    private readonly CancellationTokenSource _lifetime = new();
    private AssetDto? _asset;
    private AssetListingDto? _listing;
    private List<string> _currencies = [];
    private List<InstrumentDiscoveryResultDto>? _sources;
    private List<MarketDataSymbolResolution>? _symbols;
    private readonly MarketDataSymbolDto _manual = new();
    private bool _busy;
    private string? _savedIdentity;
    private string? _error;

    protected override Task OnInitializedAsync() => Run(async () =>
    {
        _asset = await Client.GetAsset(Item.AssetId, _lifetime.Token) ?? throw new InvalidOperationException("Asset not found. Refresh the queue.");
        _listing = _asset.Listings.FirstOrDefault(x => x.Id == Item.ListingId);
        _savedIdentity = Identity();
        _currencies = (await CurrencyClient.GetAll()).Select(x => x.ShortName).Concat(DefaultCurrency.MinorQuoteUnits.Keys).Distinct().ToList();
    });

    private Task FindSources() => Run(async () =>
    {
        var query = !string.IsNullOrWhiteSpace(_asset!.Isin) ? _asset.Isin : _listing?.Ticker ?? _asset.Name;
        _sources = (await Client.SearchExternal(query, _lifetime.Token)).Where(source =>
            Compatible(_asset.Isin, source.Isin) && Compatible(_asset.ShareClassFigi, source.ShareClassFigi) && Compatible(_asset.CompositeFigi, source.CompositeFigi)
            && (_listing is null || Compatible(_listing.Ticker, source.Ticker)
                && Compatible(_listing.ExchangeMic, source.ExchangeMic) && Compatible(_listing.TradingCurrency, source.TradingCurrency) && Compatible(_listing.ListingFigi, source.ListingFigi))).ToList();
    });

    private Task Review(InstrumentDiscoveryResultDto source) => Run(async () =>
    {
        if (_asset is null) return;
        if (_listing is not null && (!Compatible(_listing.Ticker, source.Ticker)
            || !Compatible(_listing.ExchangeMic, source.ExchangeMic) || !Compatible(_listing.TradingCurrency, source.TradingCurrency)
            || !Compatible(_listing.ListingFigi, source.ListingFigi)))
            throw new InvalidOperationException("Listing details changed. Complete from sources again before reviewing a suggestion.");
        var draft = await Client.GetExternalDraft(source, _lifetime.Token);
        if (!Compatible(_asset.Isin, draft.Asset.Isin) || !Compatible(_asset.ShareClassFigi, draft.Asset.ShareClassFigi) || !Compatible(_asset.CompositeFigi, draft.Asset.CompositeFigi))
            throw new InvalidOperationException("The source resolved to a different asset identity. Review it in the full editor.");
        if (string.IsNullOrWhiteSpace(_asset.Isin)) _asset.Isin = draft.Asset.Isin;
        if (string.IsNullOrWhiteSpace(_asset.ShareClassFigi)) _asset.ShareClassFigi = draft.Asset.ShareClassFigi;
        if (string.IsNullOrWhiteSpace(_asset.CompositeFigi)) _asset.CompositeFigi = draft.Asset.CompositeFigi;
        if (_asset.Type == AssetType.Other) _asset.Type = draft.Asset.Type;
        if (_listing is not null)
        {
            if (string.IsNullOrWhiteSpace(_listing.ExchangeMic)) _listing.ExchangeMic = source.ExchangeMic ?? string.Empty;
            if (string.IsNullOrWhiteSpace(_listing.TradingCurrency))
            {
                _listing.TradingCurrency = source.TradingCurrency ?? string.Empty;
                if (_listing.PriceMultiplier == 1m && DefaultCurrency.MinorQuoteUnits.ContainsKey(_listing.TradingCurrency)) _listing.PriceMultiplier = 0.01m;
            }
            if (string.IsNullOrWhiteSpace(_listing.ListingFigi)) _listing.ListingFigi = source.ListingFigi;
            if (string.IsNullOrWhiteSpace(_listing.ExchangeName)) _listing.ExchangeName = source.ExchangeName ?? string.Empty;
        }
        _sources = null;
    });

    private Task SaveMetadata() => Run(async () =>
    {
        if (!await Client.UpdateAsset(_asset!.Id, _asset, _lifetime.Token))
            throw new InvalidOperationException("Asset fields could not be saved. Check the values or use the full editor.");
        if (_listing is not null && !await Client.UpdateListing(_listing.Id, _listing, _lifetime.Token))
            throw new InvalidOperationException("Asset fields saved, but listing fields could not be saved. Check the venue, currency and multiplier, then retry.");
        await Saved.InvokeAsync();
    });

    private Task FindSymbols() => Run(async () =>
    {
        EnsureSaved();
        _symbols = await Client.FindExternalSymbols(new()
        {
            DisplayName = _asset!.Name,
            SecurityType = _asset.Type.ToString(),
            Isin = _asset.Isin,
            ShareClassFigi = _asset.ShareClassFigi,
            Ticker = _listing!.Ticker,
            ExchangeMic = _listing.ExchangeMic,
            TradingCurrency = _listing.TradingCurrency,
            ListingFigi = _listing.ListingFigi
        }, _listing.Id, _lifetime.Token);
    });

    private Task SaveSymbol(MarketDataSymbolResolution symbol) => ValidateSymbol(new()
    { Provider = symbol.Provider, Symbol = symbol.Symbol!, ProviderExchangeCode = symbol.ExchangeCode });

    private Task ValidateSymbol(MarketDataSymbolDto symbol) => Run(async () =>
    {
        EnsureSaved();
        await Client.ValidateQueueSymbol(_listing!.Id, symbol, _lifetime.Token);
        await Saved.InvokeAsync();
    });

    private static bool Compatible(string? current, string? proposed) =>
        string.IsNullOrWhiteSpace(current) || string.IsNullOrWhiteSpace(proposed)
        || string.Equals(current, proposed, StringComparison.OrdinalIgnoreCase);

    private string Identity() => $"{_asset?.Isin}|{_asset?.ShareClassFigi}|{_asset?.CompositeFigi}|{_asset?.Type}|{_listing?.Ticker}|{_listing?.ExchangeMic}|{_listing?.TradingCurrency}|{_listing?.ListingFigi}|{_listing?.PriceMultiplier}";
    private void EnsureSaved()
    {
        if (_savedIdentity != Identity()) throw new InvalidOperationException("Save reviewed fields before finding or validating symbols.");
    }

    private async Task Run(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _error = null;
        try { await action(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { _error = ex.Message; }
        finally { _busy = false; }
    }
    public void Dispose() { _lifetime.Cancel(); _lifetime.Dispose(); GC.SuppressFinalize(this); }
}