using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Stocks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.FinancialAccounts.StockAccountComponents.TransactionHistory;

public partial class StockTransactionRow
{
    private bool _expanded;
    private bool _updateEntryVisibility;
    private bool _removeEntryVisibility;

    [Parameter] public required StockAccount Account { get; set; }
    [Parameter] public required StockAccountEntry Entry { get; set; }
    [Parameter] public bool IsMobile { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILogger<StockTransactionRow> Logger { get; set; }

    private void ToggleExpanded() => _expanded = !_expanded;

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " " or "Spacebar")
            ToggleExpanded();
    }

    private string GetTitle() =>
        !string.IsNullOrWhiteSpace(Entry.Ticker) ? Entry.Ticker
            : !string.IsNullOrWhiteSpace(Entry.Isin) ? Entry.Isin : "Stock";

    private string GetAvatarLabel()
    {
        var source = !string.IsNullOrWhiteSpace(Entry.Ticker) ? Entry.Ticker : Entry.Isin;
        return string.IsNullOrWhiteSpace(source) ? "?" : source.Trim()[..1].ToUpperInvariant();
    }

    private static string FormatUnits(decimal value) => value.ToString("0.####");

    private Task ShowEditOverlay()
    {
        _updateEntryVisibility = true;
        return Task.CompletedTask;
    }

    private Task ShowRemoveOverlay()
    {
        _removeEntryVisibility = true;
        return Task.CompletedTask;
    }

    private Task HideOverlay()
    {
        _updateEntryVisibility = false;
        _removeEntryVisibility = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private async Task ConfirmRemove()
    {
        _removeEntryVisibility = false;
        _expanded = false;

        try
        {
            await FinancialAccountService.RemoveEntry(Entry.EntryId, Account.AccountId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while removing entry");
        }

        await AccountDataSynchronizationService.AccountChanged();
        await InvokeAsync(StateHasChanged);
    }
}
