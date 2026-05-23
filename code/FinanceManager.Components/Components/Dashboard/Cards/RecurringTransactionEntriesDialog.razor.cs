using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class RecurringTransactionEntriesDialog
{
    private bool _isLoading;
    private List<CurrencyAccountEntry> _entries = [];

    [CascadingParameter] private IMudDialogInstance? MudDialog { get; set; }

    [Parameter] public required string Name { get; set; }
    [Parameter] public required int UserId { get; set; }
    [Parameter] public required List<RecurringTransactionEntryReference> EntryReferences { get; set; }

    [Inject] public required RecurringTransactionDetectorHttpClient RecurringTransactionDetectorHttpClient { get; set; }
    [Inject] public required ILogger<RecurringTransactionEntriesDialog> Logger { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;

        try
        {
            _entries = await RecurringTransactionDetectorHttpClient.GetEntries(UserId, EntryReferences);
            _entries = [.. _entries.OrderByDescending(e => e.PostingDate)];
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Failed to load recurring transaction entries for {Name}", Name);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void Close() => MudDialog?.Close();
}
