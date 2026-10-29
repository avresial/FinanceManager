using FinanceManager.Components.Features.Administration.HttpClients;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace FinanceManager.Components.Features.Administration.Components;

public partial class AdminAssetDiscovery : ComponentBase, IDisposable
{
    [Parameter, EditorRequired] public required AssetDto Asset { get; set; }
    [Parameter] public bool IsNew { get; set; }
    [Parameter] public IEnumerable<string> Currencies { get; set; } = [];
    [Parameter] public EventCallback<AssetDto> DraftChanged { get; set; }
    [Parameter] public EventCallback<bool> BusyChanged { get; set; }
    [Inject] public required AssetHttpClient Client { get; set; }

    private readonly CancellationTokenSource _lifetime = new();
    private string _query = string.Empty;
    private bool _busy;
    private int _symbolRequests;
    private string? _error;
    private string? _message;
    private AssetDto? _observedAsset;
    private List<InstrumentDiscoveryResultDto> _results = [];
    private List<ListingCandidate> _candidates = [];
    private readonly List<FieldChange> _fieldChanges = [];
    private bool _showUnrecognizedVenues;

    // OpenFIGI returns every MTF/OTC venue for a share class (VWCE has ~180, only a few with a known MIC),
    // so venues without a MIC stay collapsed unless selected or explicitly revealed.
    private IEnumerable<ListingCandidate> VisibleCandidates =>
        _candidates.Where(x => _showUnrecognizedVenues || x.Selected || !string.IsNullOrWhiteSpace(x.Source.ExchangeMic));

    private int HiddenCandidateCount => _candidates.Count - VisibleCandidates.Count();

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_observedAsset, Asset))
        {
            _observedAsset = Asset;
            _fieldChanges.Clear();
            _candidates.Clear();
            _showUnrecognizedVenues = false;
            _results.Clear();
            _message = null;
            _error = null;
        }
    }

    private Task SearchKeyDown(KeyboardEventArgs args) => args.Key == "Enter" ? Search() : Task.CompletedTask;

    private async Task Search() => await Run(async () =>
    {
        _results = await Client.SearchExternal(_query.Trim(), _lifetime.Token);
        _message = _results.Count == 0 ? "No external matches found. Try an ISIN or another ticker." : null;
    });

    private Task RefreshFields()
    {
        _query = !string.IsNullOrWhiteSpace(Asset.Isin) ? Asset.Isin
            : Asset.Listings.FirstOrDefault()?.Ticker ?? Asset.Name;
        return Search();
    }

    private async Task UseResult(InstrumentDiscoveryResultDto result) => await Run(async () =>
    {
        var before = $"{Asset.Name}|{Asset.Type}|{Asset.Isin}|{Asset.ShareClassFigi}";
        var draft = await Client.GetExternalDraft(result, _lifetime.Token);
        if (IsNew)
        {
            if (before != $"{Asset.Name}|{Asset.Type}|{Asset.Isin}|{Asset.ShareClassFigi}")
                throw new InvalidOperationException("The asset draft changed during the search. Choose the result again to apply it.");
            Asset.Name = draft.Asset.Name;
            Asset.Type = draft.Asset.Type;
            Asset.Isin = draft.Asset.Isin;
            Asset.ShareClassFigi = draft.Asset.ShareClassFigi;
            Asset.CompositeFigi = draft.Asset.CompositeFigi;
            _candidates = draft.Listings.Select(x => new ListingCandidate(x)).ToList();
            _showUnrecognizedVenues = false;
            var selected = _candidates.FirstOrDefault(x => Matches(x.Source, result)) ?? _candidates.First();
            selected.Selected = true;
            selected.Listing.IsPrimaryListing = true;
            Asset.Listings = [selected.Listing];
            await DraftChanged.InvokeAsync(Asset);
            _message = "Review the asset fields and selected listings, then create everything in one save.";
        }
        else
        {
            _fieldChanges.Clear();
            Offer("Name", Asset.Name, draft.Asset.Name, value => Asset.Name = value);
            if (Asset.Type != draft.Asset.Type)
                _fieldChanges.Add(new("Type", Asset.Type.ToString(), draft.Asset.Type.ToString(), () => Asset.Type = draft.Asset.Type));
            Offer("ISIN", Asset.Isin, draft.Asset.Isin, value => Asset.Isin = value);
            Offer("Share-class FIGI", Asset.ShareClassFigi, draft.Asset.ShareClassFigi, value => Asset.ShareClassFigi = value);
            Offer("Composite FIGI", Asset.CompositeFigi, draft.Asset.CompositeFigi, value => Asset.CompositeFigi = value);
            _message = _fieldChanges.Count == 0 ? "The fields supplied by this source already match. Other administrator values were preserved." : null;
        }
        _results.Clear();
    });

    private void Offer(string label, string? current, string? proposed, Action<string> apply)
    {
        if (!string.IsNullOrWhiteSpace(proposed) && !string.Equals(current, proposed, StringComparison.Ordinal))
            _fieldChanges.Add(new(label, current ?? "—", proposed, () => apply(proposed)));
    }

    private async Task AcceptField(FieldChange change)
    {
        change.Apply();
        _fieldChanges.Remove(change);
        await DraftChanged.InvokeAsync(Asset);
    }

    private async Task FindListings() => await Run(async () =>
    {
        _candidates = (await Client.FindExternalListings(Asset, _lifetime.Token)).Select(x => new ListingCandidate(x)).ToList();
        _showUnrecognizedVenues = false;
        _message = _candidates.Count == 0 ? "No missing listings were found for this asset identity." : null;
    });

    private async Task SelectListing(ListingCandidate candidate, bool selected)
    {
        candidate.Selected = selected;
        if (IsNew)
        {
            Asset.Listings = _candidates.Where(x => x.Selected).Select(x => x.Listing).ToList();
            await DraftChanged.InvokeAsync(Asset);
        }
    }

    private async Task ListingChanged(ListingCandidate candidate)
    {
        candidate.Listing.MarketDataSymbols.Clear();
        candidate.Resolved = true;
        candidate.Warning = "Listing details changed. Find symbols again for the updated listing.";
        await DraftChanged.InvokeAsync(Asset);
    }

    private async Task CandidateSymbolsChanged(ListingCandidate candidate)
    {
        candidate.Resolved = true;
        candidate.Warning = null;
        await DraftChanged.InvokeAsync(Asset);
    }

    private async Task AddListing(ListingCandidate candidate) => await Run(async () =>
    {
        var created = await Client.AddExternalListing(Asset.Id, candidate.Listing, _lifetime.Token);
        Asset.Listings.Add(created);
        _candidates.Remove(candidate);
        _message = $"Added {created.Ticker} on {created.ExchangeMic}. Other asset values were preserved.";
        await DraftChanged.InvokeAsync(Asset);
    });

    private async Task SymbolsBusyChanged(bool busy)
    {
        _symbolRequests += busy ? 1 : -1;
        await BusyChanged.InvokeAsync(_busy || _symbolRequests > 0);
    }

    private async Task Run(Func<Task> action)
    {
        if (_busy) return;
        _busy = true;
        _error = null;
        _message = null;
        await BusyChanged.InvokeAsync(true);
        try { await action(); }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception ex) { _error = ex.Message; }
        finally
        {
            _busy = false;
            await BusyChanged.InvokeAsync(_symbolRequests > 0);
        }
    }

    private static bool Matches(InstrumentDiscoveryResultDto a, InstrumentDiscoveryResultDto b) =>
        !string.IsNullOrWhiteSpace(a.ListingFigi) && a.ListingFigi == b.ListingFigi
        || a.Ticker == b.Ticker && a.ExchangeMic == b.ExchangeMic && a.TradingCurrency == b.TradingCurrency;

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed class ListingCandidate(InstrumentDiscoveryResultDto source)
    {
        public InstrumentDiscoveryResultDto Source { get; } = source;
        public AssetListingDto Listing { get; } = new()
        {
            Ticker = source.Ticker,
            ExchangeMic = source.ExchangeMic ?? string.Empty,
            ExchangeName = source.ExchangeName ?? source.ExchangeMic ?? string.Empty,
            TradingCurrency = source.TradingCurrency ?? string.Empty,
            ListingFigi = source.ListingFigi,
            PriceMultiplier = source.TradingCurrency is string currency && DefaultCurrency.MinorQuoteUnits.ContainsKey(currency) ? 0.01m : 1m
        };
        public bool Selected { get; set; }
        public bool Resolved { get; set; }
        public string? Warning { get; set; }
    }

    private sealed record FieldChange(string Name, string Current, string Proposed, Action Apply);
}