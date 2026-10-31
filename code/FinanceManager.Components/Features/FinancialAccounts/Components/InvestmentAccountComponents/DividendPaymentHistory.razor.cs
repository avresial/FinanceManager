using FinanceManager.Components.Features.FinancialAccounts.HttpClients;
using FinanceManager.Components.Shared.Components.Dialogs;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Investments.Dtos;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.InvestmentAccountComponents;

public partial class DividendPaymentHistory : ComponentBase, IDisposable
{
    [Parameter] public int AccountId { get; set; }
    [Parameter] public string DefaultCurrency { get; set; } = string.Empty;
    [Inject] public required DividendPaymentHttpClient DividendPaymentHttpClient { get; set; }
    [Inject] public required CurrencyHttpClient CurrencyHttpClient { get; set; }
    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required IDialogService DialogService { get; set; }
    private const int _historyPageSize = 20;
    private readonly RefreshVersionGate _historyGate = new();
    private CancellationTokenSource _accountLifetime = new();
    private int? _loadedAccount;
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
    private decimal _formAmount;
    private string _formCurrency = string.Empty;
    private string? _formNotes;
    private bool _savingEdit;
    private bool Busy => _isLoading || _isHistoryLoading || _savingEdit || _confirmingDelete;

    protected override async Task OnParametersSetAsync()
    {
        if (_loadedAccount == AccountId) return;
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
        _formHoldingId = 0;
        _formPaymentDate = DateTime.UtcNow.Date;
        _formAmount = 0;
        _formCurrency = _availableCurrencies.FirstOrDefault(x => string.Equals(x, DefaultCurrency, StringComparison.OrdinalIgnoreCase))
            ?? _availableCurrencies.FirstOrDefault() ?? string.Empty;
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
        _formNotes = payment.Notes;
        _formError = null;
        _editVisible = true;
    }
    private void CloseEdit()
    {
        if (_savingEdit) return;
        _editVisible = false;
        _editingPayment = null;
    }
    private bool CanSaveEdit => !_savingEdit && _formHoldingId > 0 && _formAmount > 0
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
            var request = new DividendPaymentRequest { AssetListingId = _formHoldingId, PaymentDate = DateOnly.FromDateTime(_formPaymentDate!.Value), Amount = _formAmount, Currency = _formCurrency, Notes = _formNotes };
            if (creating) await DividendPaymentHttpClient.AddAsync(account, request, ct);
            else await DividendPaymentHttpClient.UpdateAsync(account, _editingPayment!.Id, request, ct);
            if (ct.IsCancellationRequested) return;
            _editVisible = false;
            _editingPayment = null;
            Snackbar.Add(creating ? "Dividend recorded." : "Dividend updated.", Severity.Success);
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