using FinanceManager.Components.Features.FinancialAccounts.Services;
using FinanceManager.Components.Features.Transactions.Bonds.HttpClients;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Bond.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.ValueObjects;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.BondAccountComponents.TransactionHistory;

public partial class BondTransactionRow
{
    private bool _expanded;
    private bool _updateEntryVisibility;
    private bool _removeEntryVisibility;

    [Parameter] public required BondAccountEntry Entry { get; set; }
    [Parameter] public string? BondName { get; set; }
    [Parameter] public BondDetails? BondDetails { get; set; }
    [Parameter] public bool IsMobile { get; set; }

    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required BondTransactionsHttpClient BondTransactionsHttpClient { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILogger<BondTransactionRow> Logger { get; set; }

    private void ToggleExpanded() => _expanded = !_expanded;

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " " or "Spacebar")
            ToggleExpanded();
    }

    private string GetTitle() =>
        BondDetails is not null && !string.IsNullOrWhiteSpace(BondDetails.Name) ? BondDetails.Name : BondName ?? "Bond";

    private string GetAccessibleName() =>
        $"{GetTitle()}, {DateFormatter.FormatDateTime(Entry.PostingDate.ToLocalTime())}, " +
        $"change {(Entry.ValueChange >= 0 ? "+" : "")}{FormatUnits(Entry.ValueChange)} units; transaction details";

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
            await BondTransactionsHttpClient.DeleteAsync(Entry.EntryId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while removing entry");
            Snackbar.Add("Could not remove the entry. Please try again.", Severity.Error);
            _removeEntryVisibility = true;
            return;
        }

        await AccountDataSynchronizationService.AccountChanged();
        await InvokeAsync(StateHasChanged);
    }
}