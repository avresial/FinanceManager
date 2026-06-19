using FinanceManager.Application.FinancialAccounts.Stock.Resolution;
using FinanceManager.Components.HttpClients;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.Admin;

public partial class AdminAddStock : ComponentBase
{
    private enum Phase
    {
        Search,
        SelectCandidate,
        Review
    }

    private readonly List<string> _errors = [];
    private readonly List<string> _info = [];

    private Phase _phase = Phase.Search;
    private bool _isSearching;
    private bool _isSaving;

    private string _searchTerm = string.Empty;
    private IReadOnlyList<InstrumentListing> _candidates = [];

    private MudForm? _form;
    private bool _isValid;

    private string _ticker = string.Empty;
    private string _name = string.Empty;
    private string _type = string.Empty;
    private string _region = string.Empty;
    private string _currency = string.Empty;
    private string _isin = string.Empty;
    private string _alphaVantageSymbol = string.Empty;

    [Inject] public required StockPriceHttpClient StockPriceHttpClient { get; set; }
    [Inject] public required NavigationManager NavigationManager { get; set; }

    private async Task OnSearchKeyDown(KeyboardEventArgs args)
    {
        if (args.Key is "Enter" or "NumpadEnter")
            await Search();
    }

    private async Task Search()
    {
        _errors.Clear();
        _info.Clear();

        var term = _searchTerm.Trim();
        if (string.IsNullOrWhiteSpace(term))
        {
            _errors.Add("Enter a ticker or ISIN to search.");
            return;
        }

        _isSearching = true;
        try
        {
            var resolution = await StockPriceHttpClient.SearchInstrument(term);
            if (resolution is null || resolution.Kind == ResolutionKind.NoMatch)
            {
                _info.Add($"No instrument found for \"{term}\". You can enter the details manually below.");
                StartManualEntry(term);
                return;
            }

            if (resolution.Kind == ResolutionKind.Ambiguous && resolution.Candidates.Count > 0)
            {
                _candidates = resolution.Candidates;
                _phase = Phase.SelectCandidate;
                return;
            }

            if (resolution.Match is not null)
            {
                PrefillFrom(resolution.Match, term);
                _phase = Phase.Review;
                return;
            }

            // Defensive fallback: resolution succeeded but carried no usable data.
            _info.Add($"No instrument found for \"{term}\". You can enter the details manually below.");
            StartManualEntry(term);
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
        }
        finally
        {
            _isSearching = false;
        }
    }

    private void SelectCandidate(InstrumentListing listing)
    {
        PrefillFrom(listing, _searchTerm.Trim());
        _phase = Phase.Review;
    }

    private void PrefillFrom(InstrumentListing listing, string searchTerm)
    {
        _isin = listing.Isin ?? string.Empty;
        _alphaVantageSymbol = listing.AlphaVantageSymbol ?? string.Empty;
        // Ticker is the broker-facing alias. Prefer the searched term when it isn't the ISIN,
        // otherwise fall back to the Alpha Vantage symbol.
        _ticker = !string.Equals(searchTerm, _isin, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(searchTerm)
            ? searchTerm.ToUpperInvariant()
            : (listing.AlphaVantageSymbol ?? searchTerm).ToUpperInvariant();
        _name = listing.Name;
        _type = listing.Type;
        _region = listing.Exchange;
        _currency = listing.Currency;
    }

    private void StartManualEntry(string searchTerm)
    {
        // No match: seed the ticker with the search term and leave the rest for the admin to fill.
        _isin = string.Empty;
        _alphaVantageSymbol = string.Empty;
        _ticker = searchTerm.ToUpperInvariant();
        _name = string.Empty;
        _type = string.Empty;
        _region = string.Empty;
        _currency = string.Empty;
        _phase = Phase.Review;
    }

    private void BackToSearch()
    {
        _errors.Clear();
        _info.Clear();
        _candidates = [];
        _phase = Phase.Search;
    }

    private async Task AddStock()
    {
        _errors.Clear();
        _info.Clear();

        if (_form is null)
        {
            _errors.Add("Form initialization error. Please try again.");
            return;
        }

        await _form.Validate();
        if (!_form.IsValid)
        {
            _errors.Add("Please correct the validation errors before submitting.");
            return;
        }

        _isSaving = true;
        try
        {
            var result = await StockPriceHttpClient.AddStockDetails(
                _ticker.Trim(),
                _name.Trim(),
                _type.Trim(),
                _region.Trim(),
                _currency.Trim(),
                string.IsNullOrWhiteSpace(_isin) ? null : _isin.Trim(),
                string.IsNullOrWhiteSpace(_alphaVantageSymbol) ? null : _alphaVantageSymbol.Trim());
            if (result is null)
            {
                _errors.Add("Failed to add stock details.");
                return;
            }

            NavigationManager.NavigateTo("Admin/Stocks");
        }
        catch (Exception ex)
        {
            _errors.Add(ex.Message);
        }
        finally
        {
            _isSaving = false;
        }
    }

    private void Cancel()
    {
        NavigationManager.NavigateTo("Admin/Stocks");
    }
}