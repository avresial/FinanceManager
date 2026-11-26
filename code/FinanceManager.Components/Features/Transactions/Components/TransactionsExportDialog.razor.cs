using FinanceManager.Components.Features.Currencies.HttpClients;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.Dashboard.Dtos;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Transactions.Bonds.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using MudBlazor;

namespace FinanceManager.Components.Features.Transactions.Components;

/// <summary>Exports one currency's cash entries or one bond's entries as CSV. Opened from the Transactions page with its current range and filters as defaults.</summary>
public partial class TransactionsExportDialog : ComponentBase, IAsyncDisposable
{
    private readonly CancellationTokenSource _exportCts = new();
    private TransactionKind _kind = TransactionKind.Cash;
    private List<Currency> _currencies = [];
    private List<BondDetails> _bonds = [];
    private Currency? _currency;
    private BondDetails? _bond;
    private DateTime? _from;
    private DateTime? _to;
    private bool _isDownloading;
    private string? _message;
    private Severity _messageSeverity = Severity.Error;

    [CascadingParameter] private IMudDialogInstance? MudDialog { get; set; }

    /// <summary>The kind to start on. Anything but bond exports cash.</summary>
    [Parameter] public TransactionKind Kind { get; set; } = TransactionKind.Cash;

    /// <summary>The currency code to start on, e.g. the single currency filtered on the page.</summary>
    [Parameter] public string? CurrencyCode { get; set; }

    [Parameter] public DateTime RangeStart { get; set; }
    [Parameter] public DateTime RangeEnd { get; set; }

    [Inject] public required CashTransactionsHttpClient CashTransactionsHttpClient { get; set; }
    [Inject] public required BondTransactionsHttpClient BondTransactionsHttpClient { get; set; }
    [Inject] public required CurrencyHttpClient CurrencyHttpClient { get; set; }
    [Inject] public required BondDetailsHttpClient BondDetailsHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required IJSRuntime JSRuntime { get; set; }
    [Inject] public required ILogger<TransactionsExportDialog> Logger { get; set; }

    private bool CanExport => !_isDownloading && _from is not null && _to is not null && _from <= _to
        && (_kind == TransactionKind.Cash ? _currency is not null : _bond is not null);

    protected override async Task OnInitializedAsync()
    {
        _kind = Kind == TransactionKind.Bond ? TransactionKind.Bond : TransactionKind.Cash;
        _from = RangeStart.Date;
        _to = RangeEnd.Date;
        try
        {
            _currencies = [.. (await CurrencyHttpClient.GetAll()).OrderBy(x => x.ShortName)];
            var preferred = SettingsService.GetCurrency();
            _currency = _currencies.FirstOrDefault(x => string.Equals(x.ShortName, CurrencyCode, StringComparison.OrdinalIgnoreCase))
                ?? _currencies.FirstOrDefault(x => x.Id == preferred.Id)
                ?? _currencies.FirstOrDefault();
            _bonds = [.. (await BondDetailsHttpClient.GetAll()).OrderBy(x => x.Name)];
            _bond = _bonds.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load the export options.");
            Show("Could not load the export options. Close the dialog and try again.", Severity.Error);
        }
    }

    private void OnKindChanged(TransactionKind kind)
    {
        _kind = kind;
        _message = null;
    }

    private void Cancel()
    {
        // Closing the dialog also abandons a download that is still running.
        _exportCts.Cancel();
        MudDialog?.Cancel();
    }

    public ValueTask DisposeAsync()
    {
        _exportCts.Cancel();
        _exportCts.Dispose();
        return ValueTask.CompletedTask;
    }

    private async Task ExportAsync()
    {
        if (!CanExport) return;
        _isDownloading = true;
        _message = null;
        try
        {
            var (start, end) = DateRangeHelper.LocalDaysToUtc(_from!.Value, _to!.Value);
            var token = _exportCts.Token;
            var csv = _kind == TransactionKind.Cash
                ? await CashTransactionsHttpClient.ExportCsvAsync(_currency!.Id, start, end, token)
                : await BondTransactionsHttpClient.ExportCsvAsync(_bond!.Id, start, end, token);
            token.ThrowIfCancellationRequested();
            if (csv.Content.Length == 0)
            {
                Show("Nothing to export in this range.", Severity.Info);
                return;
            }

            await JSRuntime.InvokeVoidAsync("financeManager.downloadFileFromBase64", FileName(), "text/csv", Convert.ToBase64String(csv.Content));
            MudDialog?.Close(DialogResult.Ok(true));
        }
        catch (OperationCanceledException) when (_exportCts.IsCancellationRequested)
        {
            // The dialog was closed while the export ran.
        }
        catch (HttpRequestException ex)
        {
            Logger.LogError(ex, "Failed to export transactions.");
            Show(string.IsNullOrWhiteSpace(ex.Message) ? "Could not export the transactions." : ex.Message, Severity.Error);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to export transactions.");
            Show("Could not export the transactions.", Severity.Error);
        }
        finally
        {
            _isDownloading = false;
        }
    }

    // Named from the days the user picked: the server names the file from the UTC bounds, which can fall on the previous day.
    private string FileName()
    {
        var days = $"{_from!.Value:yyyyMMdd}-{_to!.Value:yyyyMMdd}";
        return _kind == TransactionKind.Cash ? $"cash-{_currency!.ShortName.ToLowerInvariant()}-{days}.csv" : $"bond-{_bond!.Id}-{days}.csv";
    }

    private void Show(string message, Severity severity)
    {
        _message = message;
        _messageSeverity = severity;
    }
}