using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Domain.Assets.Discovery;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;

public partial class InvestmentTransactionForm : ComponentBase
{
    [Parameter] public int AccountId { get; set; }
    [Parameter] public bool Visible { get; set; }
    [Parameter] public EventCallback<bool> VisibleChanged { get; set; }
    [Parameter] public InvestmentTransactionDto? Transaction { get; set; }
    [Parameter] public EventCallback OnSaved { get; set; }

    [Inject] public required InvestmentTransactionHttpClient TransactionHttpClient { get; set; }
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
    private bool _noPriceAvailable;
    private string? _error;

    protected override void OnParametersSet()
    {
        if (Visible && (!_wasVisible || _initializedTransactionId != Transaction?.Id))
            ResetForm();

        _wasVisible = Visible;
    }

    private void ResetForm()
    {
        _initializedTransactionId = Transaction?.Id;
        _editingId = Transaction?.Id;
        _selectedInstrument = Transaction is null ? null : new InvestmentInstrumentOptionDto
        {
            ResultId = $"listing:{Transaction.AssetListingId}",
            Source = InstrumentOptionSource.Existing,
            AssetListingId = Transaction.AssetListingId,
            Ticker = Transaction.Ticker,
            DisplayName = Transaction.Ticker,
            ExchangeName = Transaction.ExchangeName,
            TradingCurrency = Transaction.Currency
        };
        _formListingId = Transaction?.AssetListingId ?? 0;
        _formType = Transaction?.Type ?? InvestmentTransactionType.Buy;
        _formQuantity = Transaction?.Quantity ?? 1m;
        _formUnitPrice = Transaction?.UnitPrice ?? 0m;
        _formCurrency = Transaction?.Currency ?? string.Empty;
        _formTradeDate = Transaction?.TradeDate.ToDateTime(TimeOnly.MinValue) ?? DateTime.Today;
        _formFee = Transaction?.Fee;
        _formNotes = Transaction?.Notes;
        _noPriceAvailable = false;
        _error = null;
    }

    private bool HasInstrument => _selectedInstrument is { Source: InstrumentOptionSource.Existing, AssetListingId: > 0 }
        || _selectedInstrument is { Source: InstrumentOptionSource.External, ResultId: not null and not "" };

    private bool CanSave => HasInstrument && _formQuantity > 0 && _formUnitPrice >= 0
        && _formTradeDate is not null && !string.IsNullOrWhiteSpace(_formCurrency);

    private async Task CloseAsync() => await VisibleChanged.InvokeAsync(false);

    private async Task OnInstrumentSelectedAsync(InvestmentInstrumentOptionDto? dto)
    {
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

        _formCurrency = dto.TradingCurrency;
        if (dto.AssetListingId is long listingId)
        {
            _formListingId = listingId;
            await RefreshFormPriceAsync();
        }
        else
        {
            _formListingId = 0;
            _formUnitPrice = 0m;
            _noPriceAvailable = true;
        }
    }

    private async Task OnTradeDateChangedAsync(DateTime? value)
    {
        _formTradeDate = value;
        await RefreshFormPriceAsync();
    }

    private async Task RefreshFormPriceAsync()
    {
        if (_selectedInstrument?.AssetListingId is not long listingId || _formTradeDate is not DateTime tradeDate)
        {
            _formUnitPrice = 0m;
            return;
        }

        try
        {
            var priceInfo = await TransactionHttpClient.GetListingPriceAsync(listingId, DateOnly.FromDateTime(tradeDate));
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
            Logger.LogError(ex, "Failed to fetch price for listing {ListingId}", listingId);
            _formUnitPrice = 0m;
            _noPriceAvailable = true;
        }
    }

    private async Task<IEnumerable<InvestmentInstrumentOptionDto>> SearchInstrumentsAsync(string value, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try
        {
            return await TransactionHttpClient.SearchInstrumentsAsync(value, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to search instrument listings for '{Query}'", value);
            return [];
        }
    }

    private async Task SaveAsync()
    {
        if (!CanSave) return;
        _saving = true;
        _error = null;
        try
        {
            var tradeDate = DateOnly.FromDateTime(_formTradeDate!.Value);
            var ok = _editingId is long id
                ? await TransactionHttpClient.UpdateAsync(new UpdateInvestmentTransactionRequest(
                    id, AccountId, _formListingId, _formType, _formQuantity, _formUnitPrice, _formCurrency, tradeDate, _formFee, _formNotes))
                : await TransactionHttpClient.AddAsync(new AddInvestmentTransactionRequest(
                    AccountId,
                    _selectedInstrument?.Source == InstrumentOptionSource.Existing ? _formListingId : null,
                    _formType,
                    _formQuantity,
                    _formUnitPrice,
                    _formCurrency,
                    tradeDate,
                    _formFee,
                    _formNotes,
                    _selectedInstrument?.Source == InstrumentOptionSource.External ? _selectedInstrument.ResultId : null)) is not null;

            if (!ok)
            {
                _error = "Could not save the transaction.";
                Snackbar.Add(_error, Severity.Error);
                return;
            }

            Snackbar.Add(_editingId is null ? "Transaction added." : "Transaction updated.", Severity.Success);
            await OnSaved.InvokeAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save investment transaction for account {AccountId}", AccountId);
            _error = ex is HttpRequestException && !string.IsNullOrWhiteSpace(ex.Message)
                ? ex.Message
                : "Could not save the transaction.";
            Snackbar.Add(_error, Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private static string GetSourceLabel(InstrumentOptionSource source) =>
        source == InstrumentOptionSource.Existing ? "Existing" : "External";
}