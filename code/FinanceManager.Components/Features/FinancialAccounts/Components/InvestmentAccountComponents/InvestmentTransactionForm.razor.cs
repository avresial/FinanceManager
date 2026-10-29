using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;

public partial class InvestmentTransactionForm : ComponentBase
{
    private const string _unpricedInstrumentAddedMessage =
        "Added. Automatic prices aren't available yet; an administrator has been notified.";

    [Parameter] public int AccountId { get; set; }
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public InvestmentTransactionDto? Transaction { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }

    [Inject] public required InvestmentTransactionHttpClient TransactionHttpClient { get; set; }
    [Inject] public required CurrencyHttpClient CurrencyHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required ILogger<InvestmentTransactionForm> Logger { get; set; }

    private bool _wasVisible;
    private long? _initializedTransactionId;
    private long? _editingId;
    private InvestmentInstrumentOptionDto? _selectedInstrument;
    private long _formListingId;
    private InvestmentTransactionType _formType = InvestmentTransactionType.Buy;
    private decimal _formQuantity = 1m;
    private decimal _formUnitPrice;
    private string _formCurrency = string.Empty;
    private DateTime? _formTradeDate = DateTime.Today;
    private decimal? _formFee;
    private string? _formNotes;
    private bool _saving;
    private int _priceRequestVersion;
    private bool _isPriceLoading;
    private bool _noPriceAvailable;
    private string? _error;
    private string _lastSearchQuery = string.Empty;
    private bool _lastSearchReturnedEmpty;
    private List<string> _availableCurrencies = BuildDefaultCurrencyCodes();

    protected override async Task OnParametersSetAsync()
    {
        if (Visible && (!_wasVisible || _initializedTransactionId != Transaction?.Id))
        {
            await EnsureCurrenciesLoadedAsync();
            await ResetFormAsync();
        }

        _wasVisible = Visible;
    }

    private async Task EnsureCurrenciesLoadedAsync()
    {
        try
        {
            var fetched = await CurrencyHttpClient.GetAll();
            if (fetched.Count > 0)
            {
                _availableCurrencies = fetched
                    .Select(c => c.ShortName)
                    .Where(code => !string.IsNullOrWhiteSpace(code))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "Falling back to default currency list for InvestmentTransactionForm");
        }
    }

    private async Task ResetFormAsync()
    {
        _priceRequestVersion++;
        _isPriceLoading = false;
        _initializedTransactionId = Transaction?.Id;
        _editingId = Transaction?.Id;
        _lastSearchQuery = string.Empty;
        _lastSearchReturnedEmpty = false;
        var listingTradingCurrency = Transaction?.Currency ?? string.Empty;
        if (Transaction is not null)
        {
            try
            {
                var listing = await TransactionHttpClient.GetListingAsync(Transaction.AssetListingId);
                if (listing is not null)
                    listingTradingCurrency = listing.Currency;
            }
            catch (Exception ex)
            {
                Logger.LogDebug(ex, "Could not load listing {ListingId} while initializing edit form", Transaction.AssetListingId);
            }
        }

        _selectedInstrument = Transaction is null ? null : new InvestmentInstrumentOptionDto
        {
            ResultId = $"listing:{Transaction.AssetListingId}",
            Source = InstrumentOptionSource.Existing,
            AssetListingId = Transaction.AssetListingId,
            Ticker = Transaction.Ticker,
            DisplayName = Transaction.Ticker,
            ExchangeName = Transaction.ExchangeName,
            TradingCurrency = listingTradingCurrency
        };
        _formListingId = Transaction?.AssetListingId ?? 0;
        _formType = Transaction?.Type ?? InvestmentTransactionType.Buy;
        _formQuantity = Transaction?.Quantity ?? 1m;
        _formUnitPrice = Transaction?.UnitPrice ?? 0m;
        _formCurrency = Transaction?.Currency ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(_formCurrency)
            && !_availableCurrencies.Contains(_formCurrency, StringComparer.OrdinalIgnoreCase))
        {
            _availableCurrencies = [.. _availableCurrencies, _formCurrency];
        }

        _formTradeDate = Transaction?.TradeDate.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
        _formFee = Transaction?.Fee;
        _formNotes = Transaction?.Notes;
        _noPriceAvailable = false;
        _error = null;
    }

    private bool HasInstrument => _selectedInstrument is { Source: InstrumentOptionSource.Existing, AssetListingId: > 0 }
        || _selectedInstrument is { Source: InstrumentOptionSource.External, ResultId: not null and not "" };

    private bool IsUnsupportedExternalEdit => _editingId is not null
        && _selectedInstrument?.Source == InstrumentOptionSource.External;

    private bool RequiresManualCurrencySelection => _selectedInstrument is not null
        && string.IsNullOrWhiteSpace(_selectedInstrument.TradingCurrency);

    private bool ShowUnrecognizedInputWarning => _selectedInstrument is null
        && _lastSearchReturnedEmpty
        && !string.IsNullOrWhiteSpace(_lastSearchQuery);

    private bool CanSave => !_isPriceLoading && !IsUnsupportedExternalEdit && HasInstrument && _formQuantity > 0 && _formUnitPrice >= 0
        && _formTradeDate is not null && !string.IsNullOrWhiteSpace(_formCurrency);

    private async Task CloseAsync() => await VisibleChanged.InvokeAsync(false);

    private void OnInstrumentTextChanged(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            _lastSearchQuery = string.Empty;
            _lastSearchReturnedEmpty = false;
        }
    }

    private async Task OnInstrumentSelectedAsync(InvestmentInstrumentOptionDto? dto)
    {
        _priceRequestVersion++;
        _isPriceLoading = false;
        _selectedInstrument = dto;
        _noPriceAvailable = false;
        _error = null;
        if (dto is null)
        {
            _formListingId = 0;
            _formCurrency = string.Empty;
            _formUnitPrice = 0m;
            return;
        }

        _lastSearchReturnedEmpty = false;
        // A price loaded for the previous instrument must not carry over to one without a quote.
        _formUnitPrice = 0m;
        _formCurrency = !string.IsNullOrWhiteSpace(dto.TradingCurrency)
            ? dto.TradingCurrency
            : GetDefaultCurrencyCode();

        if (!string.IsNullOrWhiteSpace(_formCurrency)
            && !_availableCurrencies.Contains(_formCurrency, StringComparer.OrdinalIgnoreCase))
        {
            _availableCurrencies = [.. _availableCurrencies, _formCurrency];
        }

        if (dto.AssetListingId is long listingId)
        {
            _formListingId = listingId;
            await RefreshFormPriceAsync();
        }
        else
        {
            _formListingId = 0;
            _noPriceAvailable = true;
        }
    }

    private async Task OnTradeDateChangedAsync(DateTime? value)
    {
        _formTradeDate = value;
        if (_selectedInstrument?.AssetListingId is > 0)
            await RefreshFormPriceAsync();
    }

    private async Task RefreshFormPriceAsync()
    {
        var version = ++_priceRequestVersion;
        _isPriceLoading = false;
        _noPriceAvailable = false;
        if (_selectedInstrument?.AssetListingId is not long listingId || _formTradeDate is not DateTime tradeDate)
        {
            _formUnitPrice = 0m;
            return;
        }

        if (string.IsNullOrWhiteSpace(_selectedInstrument.TradingCurrency))
        {
            _noPriceAvailable = true;
            return;
        }

        _isPriceLoading = true;
        try
        {
            var priceInfo = await TransactionHttpClient.GetListingPriceAsync(listingId, DateOnly.FromDateTime(tradeDate));
            if (version != _priceRequestVersion) return;

            if (priceInfo?.LatestPrice is decimal price)
                _formUnitPrice = price;
            else
            {
                _formUnitPrice = 0m;
                _noPriceAvailable = true;
            }
        }
        catch (Exception ex)
        {
            if (version != _priceRequestVersion) return;

            Logger.LogError(ex, "Failed to fetch price for listing {ListingId}", listingId);
            _formUnitPrice = 0m;
            _noPriceAvailable = true;
        }
        finally
        {
            if (version == _priceRequestVersion)
                _isPriceLoading = false;
        }
    }

    private async Task<IEnumerable<InvestmentInstrumentOptionDto>> SearchInstrumentsAsync(string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            _lastSearchQuery = string.Empty;
            _lastSearchReturnedEmpty = false;
            return [];
        }

        var trimmed = value.Trim();
        try
        {
            var results = await TransactionHttpClient.SearchInstrumentsAsync(trimmed, cancellationToken: cancellationToken);
            var filtered = (_editingId is null
                ? results
                : results.Where(x => x.Source == InstrumentOptionSource.Existing)).ToList();
            _lastSearchQuery = trimmed;
            _lastSearchReturnedEmpty = filtered.Count == 0;
            return filtered;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to search instrument listings for '{Query}'", trimmed);
            _lastSearchQuery = trimmed;
            _lastSearchReturnedEmpty = true;
            return [];
        }
    }

    private async Task SaveAsync()
    {
        if (IsUnsupportedExternalEdit)
        {
            _error = "External instruments cannot replace an existing instrument while editing.";
            Snackbar.Add(_error, Severity.Error);
            return;
        }

        if (_saving || !CanSave) return;
        _saving = true;
        _error = null;
        try
        {
            var tradeDate = DateOnly.FromDateTime(_formTradeDate!.Value);
            if (_editingId is long id)
            {
                var updated = await TransactionHttpClient.UpdateAsync(new UpdateInvestmentTransactionRequest(
                    id, AccountId, _formListingId, _formType, _formQuantity, _formUnitPrice, _formCurrency, tradeDate, _formFee, _formNotes));
                if (!updated)
                {
                    _error = "Could not save the transaction.";
                    Snackbar.Add(_error, Severity.Error);
                    return;
                }

                Snackbar.Add("Transaction updated.", Severity.Success);
            }
            else
            {
                var added = await TransactionHttpClient.AddAsync(new AddInvestmentTransactionRequest(
                    AccountId,
                    _selectedInstrument?.Source == InstrumentOptionSource.Existing ? _formListingId : null,
                    _formType,
                    _formQuantity,
                    _formUnitPrice,
                    _formCurrency,
                    tradeDate,
                    _formFee,
                    _formNotes,
                    _selectedInstrument?.Source == InstrumentOptionSource.External ? _selectedInstrument.ResultId : null));
                if (added is null)
                {
                    _error = "Could not save the transaction.";
                    Snackbar.Add(_error, Severity.Error);
                    return;
                }

                if (!added.AutomaticPricesAvailable)
                    Snackbar.Add(_unpricedInstrumentAddedMessage, Severity.Info);
                else
                    Snackbar.Add("Transaction added.", Severity.Success);
            }

            await OnSaved.InvokeAsync();
        }
        catch (HttpRequestException ex) when (!string.IsNullOrWhiteSpace(ex.Message))
        {
            Logger.LogWarning(ex, "Rejected investment transaction save for account {AccountId}", AccountId);
            _error = ex.Message;
            Snackbar.Add(_error, Severity.Error);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save investment transaction for account {AccountId}", AccountId);
            _error = "Could not save the transaction. Please try again.";
            Snackbar.Add(_error, Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private string GetDefaultCurrencyCode()
    {
        var preferred = SettingsService.GetCurrency()?.ShortName;
        if (!string.IsNullOrWhiteSpace(preferred))
            return preferred;

        return _availableCurrencies.FirstOrDefault() ?? DefaultCurrency.USD.ShortName;
    }

    private static List<string> BuildDefaultCurrencyCodes() =>
        new[] { DefaultCurrency.PLN.ShortName, DefaultCurrency.USD.ShortName }
            .Concat(DefaultCurrency.CommonCurrencies.Select(c => c.ShortName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
            .ToList();

    internal static string FormatInstrumentSubtitle(InvestmentInstrumentOptionDto option)
    {
        var hasName = !string.IsNullOrWhiteSpace(option.ExchangeName);
        var hasMic = !string.IsNullOrWhiteSpace(option.ExchangeMic);
        var venue = (hasName, hasMic) switch
        {
            (true, true) when !string.Equals(option.ExchangeName, option.ExchangeMic, StringComparison.OrdinalIgnoreCase) =>
                $"{option.ExchangeName} ({option.ExchangeMic})",
            (true, _) => option.ExchangeName,
            (false, true) => option.ExchangeMic!,
            _ => "Unknown venue"
        };

        var currency = !string.IsNullOrWhiteSpace(option.TradingCurrency)
            ? option.TradingCurrency
            : "Unknown currency";

        return $"{venue} · {currency} · {GetSourceLabel(option.Source)}";
    }

    private static string GetSourceLabel(InstrumentOptionSource source) =>
        source == InstrumentOptionSource.Existing ? "Existing" : "External";
}