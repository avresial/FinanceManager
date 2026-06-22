using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.InvestmentAccountComponents;

public partial class InvestmentAccountDetailsPageContent : ComponentBase
{
    [Parameter] public required int AccountId { get; set; }

    [Inject] public required InvestmentTransactionHttpClient TransactionHttpClient { get; set; }
    [Inject] public required InvestmentValuationHttpClient ValuationHttpClient { get; set; }
    [Inject] public required StockAccountHttpClient StockAccountHttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required ILogger<InvestmentAccountDetailsPageContent> Logger { get; set; }

    private bool _isLoading = true;
    private string _accountName = "Investments";
    private string _currency = "USD";
    private decimal _totalValue;
    private IReadOnlyList<InvestmentTransactionDto> _transactions = [];
    private List<HoldingRow> _holdings = [];
    private List<ListingOption> _listingOptions = [];

    // Add/edit overlay state.
    private bool _formVisible;
    private long? _editingId;
    private long _formListingId;
    private InvestmentTransactionType _formType = InvestmentTransactionType.Buy;
    private decimal _formQuantity = 1m;
    private decimal _formUnitPrice;
    private string _formCurrency = "USD";
    private DateTime? _formTradeDate = DateTime.Today;
    private decimal? _formFee;
    private string? _formNotes;
    private bool _saving;

    protected override async Task OnParametersSetAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        _isLoading = true;
        // Clear prior state so a failed reload (or an account switch) never shows stale data.
        _transactions = [];
        _holdings = [];
        _listingOptions = [];
        _totalValue = 0m;
        _currency = "USD";
        try
        {
            _accountName = (await StockAccountHttpClient.GetAccountAsync(AccountId))?.Name ?? "Investments";
            _transactions = await TransactionHttpClient.GetByAccountAsync(AccountId);
            var holdings = await ValuationHttpClient.GetHoldingsAsync(AccountId, DateTime.Today);

            // Latest trade per listing supplies the display ticker/exchange/currency and a price proxy for
            // valuation. (Server-side market valuation is available via the valuation API; this view stays
            // offline-deterministic by using the most recent trade price.)
            var latestByListing = _transactions
                .GroupBy(t => t.AssetListingId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.TradeDate).ThenByDescending(t => t.Id).First());

            _listingOptions = latestByListing.Values
                .Select(t => new ListingOption(t.AssetListingId, t.Ticker, t.ExchangeName, t.Currency))
                .OrderBy(o => o.Ticker)
                .ToList();

            _holdings = [];
            foreach (var (listingId, quantity) in holdings)
            {
                if (quantity == 0m || !latestByListing.TryGetValue(listingId, out var latest)) continue;
                _holdings.Add(new HoldingRow(listingId, latest.Ticker, latest.ExchangeName, latest.Currency, quantity, latest.UnitPrice, quantity * latest.UnitPrice));
            }
            _holdings = [.. _holdings.OrderByDescending(h => h.Value)];

            _totalValue = _holdings.Sum(h => h.Value);
            _currency = _holdings.FirstOrDefault()?.Currency ?? _listingOptions.FirstOrDefault()?.Currency ?? "USD";
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
        var first = _listingOptions.FirstOrDefault();
        _formListingId = first?.ListingId ?? 0;
        _formType = InvestmentTransactionType.Buy;
        _formQuantity = 1m;
        _formUnitPrice = _holdings.FirstOrDefault()?.LatestPrice ?? 0m;
        _formCurrency = first?.Currency ?? _currency;
        _formTradeDate = DateTime.Today;
        _formFee = null;
        _formNotes = null;
        _formVisible = true;
    }

    private void ShowEdit(InvestmentTransactionDto tx)
    {
        _editingId = tx.Id;
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

    private bool CanSave => _formListingId > 0 && _formQuantity > 0 && _formUnitPrice >= 0 && _formTradeDate is not null;

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

    private static string TypeLabel(InvestmentTransactionType type) => type == InvestmentTransactionType.Sell ? "Sell" : "Buy";
    private static Color TypeColor(InvestmentTransactionType type) => type == InvestmentTransactionType.Sell ? Color.Error : Color.Success;

    private sealed record HoldingRow(long ListingId, string Ticker, string ExchangeName, string Currency, decimal Quantity, decimal LatestPrice, decimal Value);
    private sealed record ListingOption(long ListingId, string Ticker, string ExchangeName, string Currency);
}