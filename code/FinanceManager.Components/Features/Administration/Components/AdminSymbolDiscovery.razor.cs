using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Administration.Components;

public partial class AdminSymbolDiscovery : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required AssetDto Asset { get; set; }
    [Parameter, EditorRequired] public required AssetListingDto Listing { get; set; }
    [Parameter] public InstrumentDiscoveryResultDto? Source { get; set; }
    [Parameter] public bool IsDraft { get; set; }
    [Parameter] public bool AutoResolve { get; set; }
    [Parameter] public EventCallback Changed { get; set; }
    [Parameter] public EventCallback<bool> BusyChanged { get; set; }
    [Inject] public required AssetHttpClient Client { get; set; }

    private readonly CancellationTokenSource _lifetime = new();
    private bool _started;
    private bool _busy;
    private string? _error;
    private List<MarketDataSymbolResolution>? _results;
    private string? _resultIdentity;

    protected override async Task OnParametersSetAsync()
    {
        if (IsDraft && AutoResolve && !_started)
        {
            _started = true;
            await FindSymbols(true);
        }
    }

    private InstrumentDiscoveryResultDto Instrument() => new()
    {
        DisplayName = Asset.Name,
        SecurityType = Asset.Type.ToString(),
        Isin = Asset.Isin,
        ShareClassFigi = Asset.ShareClassFigi,
        CompositeFigi = Asset.CompositeFigi,
        Ticker = Listing.Ticker,
        ExchangeMic = Listing.ExchangeMic,
        ExchangeName = Listing.ExchangeName,
        TradingCurrency = Listing.TradingCurrency,
        ListingFigi = Listing.ListingFigi,
        ProviderSymbol = Source is not null && string.Equals(Source.Ticker, Listing.Ticker, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Source.ExchangeMic, Listing.ExchangeMic, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Source.TradingCurrency, Listing.TradingCurrency, StringComparison.OrdinalIgnoreCase) ? Source.ProviderSymbol : null
    };

    private static string Identity(InstrumentDiscoveryResultDto x) =>
        $"{x.Ticker}|{x.ExchangeMic}|{x.TradingCurrency}|{x.ListingFigi}|{x.Isin}|{x.ShareClassFigi}|{x.DisplayName}|{x.SecurityType}";

    private async Task FindSymbols(bool acceptDraft)
    {
        if (_busy) return;
        _busy = true;
        _error = null;
        await BusyChanged.InvokeAsync(true);
        var instrument = Instrument();
        try
        {
            var results = await Client.FindExternalSymbols(instrument, IsDraft ? null : Listing.Id, _lifetime.Token);
            if (Identity(instrument) != Identity(Instrument()))
            {
                _error = "Listing details changed. Find symbols again for the updated listing.";
                _results = null;
                return;
            }
            _results = results;
            _resultIdentity = Identity(instrument);
            if (acceptDraft)
            {
                foreach (var result in results.Where(x => x.Status == "resolved" && !string.IsNullOrWhiteSpace(x.Symbol)).OrderBy(x => x.Priority))
                    if (!Exists(result)) Listing.MarketDataSymbols.Add(ToSymbol(result));
                await Changed.InvokeAsync();
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { _error = ex.Message; }
        finally
        {
            _busy = false;
            await BusyChanged.InvokeAsync(false);
        }
    }

    private bool Exists(MarketDataSymbolResolution result) => Listing.MarketDataSymbols.Any(x =>
        x.Provider == result.Provider && string.Equals(x.Symbol, result.Symbol, StringComparison.OrdinalIgnoreCase));

    private MarketDataSymbolDto ToSymbol(MarketDataSymbolResolution result) => new()
    {
        Provider = result.Provider,
        Symbol = result.Symbol!,
        ProviderExchangeCode = result.ExchangeCode,
        Currency = result.Currency,
        IsEnabled = true,
        IsPrimary = !Listing.MarketDataSymbols.Any(x => x.IsPrimary && x.IsEnabled)
    };

    private async Task UseSymbol(MarketDataSymbolResolution result)
    {
        if (_busy || Exists(result)) return;
        if (_resultIdentity != Identity(Instrument()))
        {
            _error = "Listing details changed. Find symbols again before using a candidate.";
            _results = null;
            return;
        }
        _busy = true;
        _error = null;
        try
        {
            var symbol = ToSymbol(result);
            if (!IsDraft) symbol = await Client.CreateSymbol(Listing.Id, symbol, _lifetime.Token)
                ?? throw new InvalidOperationException("The provider symbol could not be added.");
            Listing.MarketDataSymbols.Add(symbol);
            await Changed.InvokeAsync();
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { _error = ex.Message; }
        finally { _busy = false; }
    }

    private async Task Remove(MarketDataSymbolDto symbol)
    {
        Listing.MarketDataSymbols.Remove(symbol);
        await Changed.InvokeAsync();
    }

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}