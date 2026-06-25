using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Assets.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.InvestmentAccountComponents;

public partial class InvestmentAccountDetailsPageContent : ComponentBase, IAsyncDisposable
{
    [Parameter] public required int AccountId { get; set; }

    [Inject] public required InvestmentTransactionHttpClient TransactionHttpClient { get; set; }
    [Inject] public required InvestmentValuationHttpClient ValuationHttpClient { get; set; }
    [Inject] public required InvestmentAccountHttpClient InvestmentAccountHttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required IBrowserViewportService BrowserViewportService { get; set; }
    [Inject] public required ILogger<InvestmentAccountDetailsPageContent> Logger { get; set; }

    private readonly Guid _viewportSubscriptionId = Guid.NewGuid();
    private bool _isMobile;
    private bool _isLoading = true;
    private string _accountName = "Investments";
    private string _currency = "USD";
    private decimal _totalValue;
    private IReadOnlyList<InvestmentTransactionDto> _transactions = [];
    private List<HoldingRow> _holdings = [];

    // Add/edit overlay state.
    private bool _formVisible;
    private long? _editingId;
    private InstrumentSearchResultDto? _selectedInstrument;
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

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;

        await BrowserViewportService.SubscribeAsync(_viewportSubscriptionId, async args =>
        {
            var isMobile = args.Breakpoint is Breakpoint.Xs or Breakpoint.Sm or Breakpoint.Md;
            if (isMobile == _isMobile) return;
            _isMobile = isMobile;
            await InvokeAsync(StateHasChanged);
        }, fireImmediately: true);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await BrowserViewportService.UnsubscribeAsync(_viewportSubscriptionId);
        }
        catch (Exception ex)
        {
            Logger.LogDebug(ex, "Failed to unsubscribe from BrowserViewportService");
        }
        GC.SuppressFinalize(this);
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        _transactions = [];
        _holdings = [];
        _totalValue = 0m;
        _currency = "USD";
        try
        {
            _accountName = (await InvestmentAccountHttpClient.GetAccountAsync(AccountId))?.Name ?? "Investments";
            _transactions = await TransactionHttpClient.GetByAccountAsync(AccountId);
            var holdings = await ValuationHttpClient.GetHoldingsAsync(AccountId, DateTime.Today);

            var latestByListing = _transactions
                .GroupBy(t => t.AssetListingId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.TradeDate).ThenByDescending(t => t.Id).First());

            _holdings = [];
            foreach (var (listingId, quantity) in holdings)
            {
                if (quantity == 0m || !latestByListing.TryGetValue(listingId, out var latest)) continue;
                _holdings.Add(new HoldingRow(listingId, latest.Ticker, latest.ExchangeName, latest.Currency, quantity, latest.UnitPrice, quantity * latest.UnitPrice));
            }
            _holdings = [.. _holdings.OrderByDescending(h => h.Value)];

            _totalValue = _holdings.Sum(h => h.Value);
            _currency = _holdings.FirstOrDefault()?.Currency ?? "USD";
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load investment account {AccountId}", AccountId);
            Snackbar.Add("Could not load the investment account.", Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void ShowAdd()
    {
        _editingId = null;
        _selectedInstrument = null;
        _formListingId = 0;
        _formType = InvestmentTransactionType.Buy;
        _formQuantity = 1m;
        _formUnitPrice = 0m;
        _formCurrency = string.Empty;
        _formTradeDate = DateTime.Today;
        _formFee = null;
        _formNotes = null;
        _noPriceAvailable = false;
        _formVisible = true;
    }

    private void ShowEdit(InvestmentTransactionDto tx)
    {
        _editingId = tx.Id;
        _selectedInstrument = new InstrumentSearchResultDto(tx.AssetListingId, tx.Ticker, tx.ExchangeName, tx.Currency);
        _formListingId = tx.AssetListingId;
        _formType = tx.Type;
        _formQuantity = tx.Quantity;
        _formUnitPrice = tx.UnitPrice;
        _formCurrency = tx.Currency;
        _formTradeDate = tx.TradeDate.ToDateTime(TimeOnly.MinValue);
        _formFee = tx.Fee;
        _formNotes = tx.Notes;
        _formVisible = true;
    }

    private void CloseForm() => _formVisible = false;

    private bool CanSave => _formListingId > 0 && _formQuantity > 0 && _formUnitPrice >= 0 && _formTradeDate is not null && !string.IsNullOrWhiteSpace(_formCurrency) && !_noPriceAvailable;

    private async Task OnInstrumentSelectedAsync(InstrumentSearchResultDto? dto)
    {
        _selectedInstrument = dto;
        _noPriceAvailable = false;
        if (dto is null)
        {
            _formListingId = 0;
            _formCurrency = string.Empty;
            _formUnitPrice = 0m;
            return;
        }

        _formListingId = dto.ListingId;
        _formCurrency = dto.Currency;

        if (_editingId is null)
        {
            try
            {
                var priceInfo = await TransactionHttpClient.GetListingPriceAsync(dto.ListingId);
                if (priceInfo?.LatestPrice is decimal price)
                {
                    _formUnitPrice = price;
                }
                else
                {
                    _formUnitPrice = 0m;
                    _noPriceAvailable = true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Failed to fetch price for listing {ListingId}", dto.ListingId);
                _formUnitPrice = 0m;
                _noPriceAvailable = true;
            }
        }
    }

    private async Task<IEnumerable<InstrumentSearchResultDto>> SearchInstrumentsAsync(string value, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(value)) return [];
        try
        {
            return await TransactionHttpClient.SearchListingsAsync(value);
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
        try
        {
            var tradeDate = DateOnly.FromDateTime(_formTradeDate!.Value);
            bool ok;
            if (_editingId is long id)
            {
                ok = await TransactionHttpClient.UpdateAsync(new UpdateInvestmentTransactionRequest(
                    id, AccountId, _formListingId, _formType, _formQuantity, _formUnitPrice, _formCurrency, tradeDate, _formFee, _formNotes));
            }
            else
            {
                var created = await TransactionHttpClient.AddAsync(new AddInvestmentTransactionRequest(
                    AccountId, _formListingId, _formType, _formQuantity, _formUnitPrice, _formCurrency, tradeDate, _formFee, _formNotes));
                ok = created is not null;
            }

            if (ok)
            {
                Snackbar.Add(_editingId is null ? "Transaction added." : "Transaction updated.", Severity.Success);
                _formVisible = false;
                await LoadAsync();
            }
            else
            {
                Snackbar.Add("Could not save the transaction.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to save investment transaction for account {AccountId}", AccountId);
            Snackbar.Add("Could not save the transaction.", Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task DeleteAsync(InvestmentTransactionDto tx)
    {
        try
        {
            if (await TransactionHttpClient.DeleteAsync(AccountId, tx.Id))
            {
                Snackbar.Add("Transaction removed.", Severity.Success);
                await LoadAsync();
            }
            else
            {
                Snackbar.Add("Could not remove the transaction.", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to delete investment transaction {TransactionId} for account {AccountId}", tx.Id, AccountId);
            Snackbar.Add("Could not remove the transaction.", Severity.Error);
        }
    }

    private sealed record HoldingRow(long ListingId, string Ticker, string ExchangeName, string Currency, decimal Quantity, decimal LatestPrice, decimal Value);
}