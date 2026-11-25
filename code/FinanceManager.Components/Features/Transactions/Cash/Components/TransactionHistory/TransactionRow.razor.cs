using FinanceManager.Components.Features.Transactions.Cash.HttpClients;
using FinanceManager.Components.Features.Transactions.Shared.Services;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.Transactions.Cash.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Features.Transactions.Cash.Components.TransactionHistory;

public partial class TransactionRow
{
    private bool _expanded;
    private bool _detailsRendered;
    private bool _updateEntryVisibility;
    private bool _removeEntryVisibility;
    private long? _loadedEntryId;
    private bool? _loadedInitiallyExpanded;

    [Parameter] public required CashEntry Entry { get; set; }
    [Parameter] public required string Currency { get; set; }
    [Parameter] public bool IsMobile { get; set; }
    [Parameter] public bool InitiallyExpanded { get; set; }

    /// <summary>True when the surrounding rows all carry the identical time, so showing it on each row says nothing.</summary>
    [Parameter] public bool SiblingsShareSameTime { get; set; }

    [Inject] public required ISnackbar Snackbar { get; set; }
    [Inject] public required CashTransactionsHttpClient CashTransactionsHttpClient { get; set; }
    [Inject] public required TransactionDataSynchronizationService TransactionDataSynchronizationService { get; set; }
    [Inject] public required ILogger<TransactionRow> Logger { get; set; }

    protected override void OnParametersSet()
    {
        if (_loadedEntryId == Entry.EntryId && _loadedInitiallyExpanded == InitiallyExpanded) return;

        _loadedEntryId = Entry.EntryId;
        _loadedInitiallyExpanded = InitiallyExpanded;
        _expanded = InitiallyExpanded;
        _detailsRendered |= _expanded;
    }

    private void ToggleExpanded()
    {
        _expanded = !_expanded;
        _detailsRendered = true;
    }

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

    private string GetAvatarIcon() =>
        TransactionRowPresentation.IconFor(Entry.Labels?.FirstOrDefault()?.Name, Entry.ValueChange);

    private bool ShowTimeLabel =>
        !IsMobile && TransactionRowPresentation.ShouldShowTime(Entry.PostingDate.ToLocalTime(), SiblingsShareSameTime);

    private bool HasMeta => (Entry.Labels is not null && Entry.Labels.Any()) || ShowTimeLabel;

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
            await CashTransactionsHttpClient.DeleteAsync(Entry.EntryId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while removing entry");
            Snackbar.Add("Could not remove the entry. Please try again.", Severity.Error);
            _removeEntryVisibility = true;
            return;
        }

        await TransactionDataSynchronizationService.AccountChanged();
        await InvokeAsync(StateHasChanged);
    }
}