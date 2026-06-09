using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.CurrencyAccountComponents.TransactionHistory;

public partial class TransactionRow
{
    private bool _expanded;
    private bool _updateEntryVisibility;
    private bool _removeEntryVisibility;

    [Parameter] public required CurrencyAccount Account { get; set; }
    [Parameter] public required CurrencyAccountEntry Entry { get; set; }
    [Parameter] public required string Currency { get; set; }
    [Parameter] public bool IsMobile { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required ILogger<TransactionRow> Logger { get; set; }

    private void ToggleExpanded() => _expanded = !_expanded;

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key is "Enter" or " " or "Spacebar")
            ToggleExpanded();
    }

    private string GetTitle()
    {
        if (!string.IsNullOrWhiteSpace(Entry.Description))
            return Truncate(Entry.Description, 80);
        if (!string.IsNullOrWhiteSpace(Entry.ContractorDetails))
            return Truncate(Entry.ContractorDetails, 80);
        if (Entry.Labels is not null && Entry.Labels.Any())
            return Entry.Labels.First().Name;
        return "Transaction";
    }

    private string GetAvatarLabel()
    {
        var firstLabel = Entry.Labels?.FirstOrDefault()?.Name;
        if (!string.IsNullOrWhiteSpace(firstLabel))
            return firstLabel.Trim()[..1].ToUpperInvariant();
        return "?";
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max] + "...";

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