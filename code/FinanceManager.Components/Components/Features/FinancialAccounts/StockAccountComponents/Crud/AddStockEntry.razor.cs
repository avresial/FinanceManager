using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Components.HttpClients;
using FinanceManager.Components.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.Identity.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.StockAccountComponents.Crud;

public partial class AddStockEntry
{
    private Currency _currency = DefaultCurrency.PLN;
    private bool _success;
    private string[] _errors = [];
    private MudForm? _form;

    private DateTime? _postingDate = DateTime.Today;
    private TimeSpan? _time = new TimeSpan(01, 00, 00);

    private string _searchTerm = string.Empty;
    private bool _resolving;
    private string? _resolveMessage;
    private InstrumentResolution? _resolution;
    private InstrumentListing? _selectedListing;
    private bool _pricePending;

    public decimal? BalanceChange;
    public decimal? PricePerUnit { get; private set; }
    public string InvestmentTypeName { get; private set; } = InvestmentType.Stock.ToString();

    [Parameter] public List<string> Tickers { get; set; } = [];
    [Parameter] public RenderFragment? CustomButton { get; set; }
    [Parameter] public EventCallback ActionCompleted { get; set; }
    [Parameter] public required StockAccount StockAccount { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required StockPriceHttpClient StockPriceHttpClient { get; set; }
    [Inject] public required ILogger<AddStockEntry> Logger { get; set; }

    protected override Task OnParametersSetAsync()
    {
        _currency = SettingsService.GetCurrency();
        return base.OnParametersSetAsync();
    }

    private Task<IEnumerable<string>> SearchTicker(string value, CancellationToken token)
    {
        if (string.IsNullOrEmpty(value)) return Task.FromResult(Tickers.AsEnumerable());

        return Task.FromResult(Tickers.Where(x => x.Contains(value, StringComparison.InvariantCultureIgnoreCase)));
    }

    /// <summary>
    /// Resolves the entered search term against the instrument-resolution endpoint.
    /// A single match auto-fills the form; ambiguous matches surface the candidate picker.
    /// </summary>
    public async Task ResolveInstrument()
    {
        if (string.IsNullOrWhiteSpace(_searchTerm)) return;

        _resolving = true;
        _resolveMessage = null;
        _resolution = null;
        _selectedListing = null;
        PricePerUnit = null;
        _pricePending = false;

        try
        {
            _resolution = await StockPriceHttpClient.SearchInstrument(_searchTerm.Trim());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error resolving instrument for '{SearchTerm}'.", _searchTerm);
        }

        if (_resolution is null || _resolution.Kind == ResolutionKind.NoMatch)
        {
            _resolveMessage = $"No instrument found for \"{_searchTerm}\".";
        }
        else if (_resolution.Kind == ResolutionKind.AutoResolved && _resolution.Match is not null)
        {
            await SelectListing(_resolution.Match);
        }

        _resolving = false;
    }

    /// <summary>Applies a confirmed listing: fills name/type/currency and fetches its price.</summary>
    public async Task SelectListing(InstrumentListing listing)
    {
        _selectedListing = listing;
        InvestmentTypeName = InstrumentTypeMapper.MapToInvestmentType(listing.Type).ToString();
        await FetchPrice();
    }

    private async Task FetchPrice()
    {
        PricePerUnit = null;
        _pricePending = false;

        if (_selectedListing is null || string.IsNullOrWhiteSpace(_selectedListing.Isin) || !_postingDate.HasValue)
        {
            _pricePending = true;
            return;
        }

        try
        {
            var price = await StockPriceHttpClient.GetStockPrice(_selectedListing.Isin, _currency.Id, _postingDate.Value);
            if (price is null)
                _pricePending = true;
            else
                PricePerUnit = price.PricePerUnit;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error fetching price for ISIN '{Isin}'.", _selectedListing.Isin);
            _pricePending = true;
        }
    }

    public async Task Add()
    {
        if (_form is null) return;
        await _form.Validate();

        if (!_form.IsValid) return;
        if (!BalanceChange.HasValue) return;
        if (!_postingDate.HasValue) return;
        if (!_time.HasValue) return;

        if (_selectedListing is null)
        {
            _errors = ["Please search for and confirm an instrument first."];
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedListing.Isin))
        {
            _errors = ["The selected instrument has no ISIN, which is required to save the entry."];
            return;
        }

        DateTime date = new(_postingDate.Value.Year, _postingDate.Value.Month, _postingDate.Value.Day, _time.Value.Hours, _time.Value.Minutes, _time.Value.Seconds);
        InvestmentType investmentType = InstrumentTypeMapper.MapToInvestmentType(_selectedListing.Type);

        var id = 0;
        var currentMaxId = StockAccount.GetMaxId();
        if (currentMaxId is not null)
            id += currentMaxId.Value + 1;

        StockAccountEntry entry = new(StockAccount.AccountId, id, date.ToUniversalTime(), -1, BalanceChange.Value, _selectedListing.Isin, investmentType)
        {
            Ticker = _searchTerm.Trim()
        };

        try
        {
            await FinancialAccountService.AddEntry(entry);
            StockAccount.Add(entry);
        }
        catch (Exception ex)
        {
            _errors = [ex.ToString()];
            return;
        }

        await ActionCompleted.InvokeAsync();
    }

    public async Task Cancel()
    {
        await ActionCompleted.InvokeAsync();
    }
}