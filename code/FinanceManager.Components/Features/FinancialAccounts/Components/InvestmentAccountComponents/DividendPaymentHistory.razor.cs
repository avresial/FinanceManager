using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Shared.Components.Dialogs;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using System.Globalization;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;

public partial class DividendPaymentHistory : ComponentBase, IDisposable
{
    [Parameter] public string? Height { get; set; }
    [Parameter] public int? AccountId { get; set; }
    [Parameter] public string DefaultCurrency { get; set; } = string.Empty;
    [Inject] public required DividendPaymentHttpClient DividendPaymentHttpClient { get; set; }
    [Inject] public required CurrencyHttpClient CurrencyHttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }
    // Publishes that holdings or dividends changed, so the navigation menu re-evaluates its cached "Assets" link.
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronization { get; set; }
    private const int _historyPageSize = 20;
    private readonly RefreshVersionGate _historyGate = new();
    private CancellationTokenSource _accountLifetime = new();
    private int? _loadedAccount;
    private bool _initialized;
    private List<DividendPaymentDto> _payments = [];
    private List<DividendHoldingOption> _holdings = [];
    private List<string> _availableCurrencies = [];
    private bool _isLoading;
    private bool _isHistoryLoading;
    private int _historyPage = 1;
    private bool _hasMore;
    private string? _error;
    private string? _formError;
    private bool _editVisible;
    private bool _confirmingDelete;
    private DividendPaymentDto? _editingPayment;
    private long _formHoldingId;
    private DateTime? _formPaymentDate;
    private decimal? _formAmount;
    // Once the user picks a currency themselves, changing the holding no longer overrides it.
    private bool _currencyChosen;
    private string _formCurrency = string.Empty;
    private string? _formNotes;
    private bool _savingEdit;
    private bool Busy => _isLoading || _isHistoryLoading || _savingEdit || _confirmingDelete;

    protected override async Task OnParametersSetAsync()
    {
        if (_initialized && _loadedAccount == AccountId) return;
        _initialized = true;
        _loadedAccount = AccountId;
        _accountLifetime.Cancel();
        _accountLifetime.Dispose();
        _accountLifetime = new();
        _historyGate.Claim();
        _payments = [];
        _holdings = [];
        _availableCurrencies = [];
        _editVisible = false;
        _savingEdit = false;
        _confirmingDelete = false;
        await ReloadHistoryAsync();
    }

    private async Task ReloadHistoryAsync()
    {
        var version = _historyGate.Claim();
        var account = AccountId;
        var ct = _accountLifetime.Token;
        _isLoading = true;
        _isHistoryLoading = false;
        _error = null;
        try
        {
            var pageTask = DividendPaymentHttpClient.GetHistoryPageAsync(account, 1, _historyPageSize, ct);
            var holdingsTask = DividendPaymentHttpClient.GetHoldingsAsync(account, ct);
            var currenciesTask = CurrencyHttpClient.GetAll(ct);
            await Task.WhenAll(pageTask, holdingsTask, currenciesTask);
            if (ct.IsCancellationRequested || !_historyGate.IsCurrent(version)) return;
            var page = await pageTask;
            _payments = [.. page.Items];
            _holdings = [.. await holdingsTask];
            _availableCurrencies = (await currenciesTask).Select(x => x.ShortName)
                .Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Order().ToList();
            _historyPage = page.Page;
            _hasMore = (long)page.Page * page.PageSize < page.TotalCount;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception)
        {
            if (_historyGate.IsCurrent(version)) _error = "Could not load dividend payments. Retry to refresh the history and available holdings.";
        }
        finally { if (_historyGate.IsCurrent(version)) _isLoading = false; }
    }

    private async Task LoadOlderPaymentsAsync()
    {
        if (!_hasMore || Busy) return;
        var version = _historyGate.Claim();
        var ct = _accountLifetime.Token;
        _isHistoryLoading = true;
        _error = null;
        try
        {
            var page = await DividendPaymentHttpClient.GetHistoryPageAsync(AccountId, _historyPage + 1, _historyPageSize, ct);
            if (ct.IsCancellationRequested || !_historyGate.IsCurrent(version)) return;
            var known = _payments.Select(x => x.Id).ToHashSet();
            _payments.AddRange(page.Items.Where(x => known.Add(x.Id)));
            _historyPage = page.Page;
            _hasMore = (long)page.Page * page.PageSize < page.TotalCount;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception) { if (_historyGate.IsCurrent(version)) _error = "Could not load older dividend payments. Retry to refresh the history."; }
        finally { if (_historyGate.IsCurrent(version)) _isHistoryLoading = false; }
    }

    private void ShowRecord()
    {
        if (Busy) return;
        _editingPayment = null;
        _formHoldingId = _holdings.FirstOrDefault()?.AssetListingId ?? 0;
        _formPaymentDate = DateTime.UtcNow.Date;
        _formAmount = null;
        _currencyChosen = false;
        _formCurrency = CurrencyFor(_formHoldingId);
        _formNotes = null;
        _formError = null;
        _editVisible = true;
    }
    private void ShowEdit(DividendPaymentDto payment)
    {
        if (Busy) return;
        _editingPayment = payment;
        _formHoldingId = payment.AssetListingId;
        _formPaymentDate = payment.PaymentDate.ToDateTime(TimeOnly.MinValue);
        _formAmount = payment.Amount;
        _formCurrency = payment.Currency;
        _currencyChosen = true;
        _formNotes = payment.Notes;
        _formError = null;
        _editVisible = true;
    }
    // A payout normally arrives in the currency the holding trades in; fall back to the display currency, then the first known one.
    private string CurrencyFor(long listingId)
    {
        var tradingCurrency = _holdings.FirstOrDefault(x => x.AssetListingId == listingId)?.TradingCurrency;
        return FindCurrency(tradingCurrency) ?? FindCurrency(DefaultCurrency) ?? _availableCurrencies.FirstOrDefault() ?? string.Empty;
    }

    private string? FindCurrency(string? shortName) =>
        string.IsNullOrWhiteSpace(shortName) ? null : _availableCurrencies.FirstOrDefault(x => string.Equals(x, shortName.Trim(), StringComparison.OrdinalIgnoreCase));

    // Without this the closed select shows the raw listing id until its items have rendered.
    private string HoldingLabel(long listingId) =>
        _holdings.FirstOrDefault(x => x.AssetListingId == listingId) is { } holding ? $"{holding.Name} ({holding.Ticker})" : string.Empty;

    private void OnHoldingChanged(long listingId)
    {
        _formHoldingId = listingId;
        if (!_currencyChosen) _formCurrency = CurrencyFor(listingId);
    }

    private void OnCurrencyChanged(string currency)
    {
        _formCurrency = currency;
        _currencyChosen = true;
    }

    private void OnDialogKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape") CloseEdit();
    }

    // The header names what the card holds; the total is only honest while every payment is loaded, and each currency keeps its own sum.
    private string Subtitle
    {
        get
        {
            if (_isLoading || _payments.Count == 0) return "Cash received from your holdings";
            if (_hasMore) return "Latest payments";
            var totals = _payments.GroupBy(x => x.Currency, StringComparer.OrdinalIgnoreCase).OrderBy(x => x.Key, StringComparer.OrdinalIgnoreCase)
                .Select(x => MoneyFormatter.Format(x.Sum(p => p.Amount), x.Key));
            return $"{(_payments.Count == 1 ? "1 payment" : $"{_payments.Count} payments")} · total {string.Join(" · ", totals)}";
        }
    }

    private void CloseEdit()
    {
        if (_savingEdit) return;
        _editVisible = false;
        _editingPayment = null;
    }
    // Keep the ledger's accepted precision while retaining the usual two decimal places for whole amounts.
    private static string FormatCashReceived(DividendPaymentDto payment) =>
        $"{payment.Amount.ToString("#,##0.00######", CultureInfo.InvariantCulture)} {payment.Currency}";

    private bool CanSaveEdit => !_savingEdit && _formHoldingId > 0 && _formAmount is decimal amount && amount > 0
        && !string.IsNullOrWhiteSpace(_formCurrency) && _formPaymentDate is DateTime date && date <= DateTime.UtcNow.Date;

    private async Task SaveEditAsync()
    {
        if (!CanSaveEdit) return;
        var account = AccountId;
        var ct = _accountLifetime.Token;
        var creating = _editingPayment is null;
        _savingEdit = true;
        _formError = null;
        try
        {
            var request = new DividendPaymentRequest { AssetListingId = _formHoldingId, PaymentDate = DateOnly.FromDateTime(_formPaymentDate!.Value), Amount = _formAmount!.Value, Currency = _formCurrency, Notes = _formNotes };
            if (creating) await DividendPaymentHttpClient.AddAsync(account, request, ct);
            else await DividendPaymentHttpClient.UpdateAsync(account, _editingPayment!.Id, request, ct);
            if (ct.IsCancellationRequested) return;
            _editVisible = false;
            _editingPayment = null;
            Snackbar.Add(creating ? "Dividend recorded." : "Dividend updated.", Severity.Success);
            await AccountDataSynchronization.AccountChanged();
            await ReloadHistoryAsync();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (HttpRequestException ex) { if (!ct.IsCancellationRequested) { _formError = ex.Message; Snackbar.Add(_formError, Severity.Error); } }
        catch (Exception) { if (!ct.IsCancellationRequested) _formError = "Could not confirm the save. Refresh the history before retrying."; }
        finally { if (!ct.IsCancellationRequested) _savingEdit = false; }
    }

    private async Task ConfirmDeleteAsync(DividendPaymentDto payment)
    {
        if (Busy) return;
        var account = AccountId;
        var ct = _accountLifetime.Token;
        _confirmingDelete = true;
        try
        {
            var parameters = new DialogParameters<ConfirmDeleteDialog> { { x => x.Title, "Delete dividend" }, { x => x.Message, $"Delete {payment.Ticker} dividend received on {DateFormatter.Format(payment.PaymentDate)}?" } };
            var dialog = await DialogService.ShowAsync<ConfirmDeleteDialog>("Delete dividend", parameters);
            var result = await dialog.Result;
            if (ct.IsCancellationRequested || result is null || result.Canceled) return;
            await DividendPaymentHttpClient.DeleteAsync(account, payment.Id, ct);
            if (ct.IsCancellationRequested) return;
            Snackbar.Add("Dividend removed.", Severity.Success);
            await AccountDataSynchronization.AccountChanged();
            await ReloadHistoryAsync();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (HttpRequestException ex) { if (!ct.IsCancellationRequested) Snackbar.Add(ex.Message, Severity.Error); }
        finally { if (!ct.IsCancellationRequested) _confirmingDelete = false; }
    }
    public void Dispose()
    {
        _historyGate.Claim();
        _accountLifetime.Cancel();
        _accountLifetime.Dispose();
        GC.SuppressFinalize(this);
    }
}