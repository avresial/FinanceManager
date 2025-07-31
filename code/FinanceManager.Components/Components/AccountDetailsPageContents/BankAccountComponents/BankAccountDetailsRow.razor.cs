using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.BankAccountComponents;

public partial class BankAccountDetailsRow
{
    private bool _expanded = false;
    private bool _removeEntryVisibility;
    private bool _updateEntryVisibility;
    internal string _currency = DefaultCurrency.Currency;

    [Parameter] public required BankAccount BankAccount { get; set; }
    [Parameter] public required BankAccountEntry BankAccountEntry { get; set; }

    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILogger<BankAccountDetailsRow> Logger { get; set; }

    protected override void OnParametersSet()
    {
        _currency = SettingsService.GetCurrency();
    }

    public async Task Confirm()
    {
        _updateEntryVisibility = false;
        _removeEntryVisibility = false;
        _expanded = false;

        try
        {
            await FinancialAccountService.RemoveEntry(BankAccountEntry.EntryId, BankAccount.AccountId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while removing entry");
        }

        await AccountDataSynchronizationService.AccountChanged();
        await InvokeAsync(StateHasChanged);
    }

    public async Task Cancel()
    {
        _updateEntryVisibility = false;
        _removeEntryVisibility = false;
        await InvokeAsync(StateHasChanged);
    }

    public async Task HideOverlay()
    {
        _updateEntryVisibility = false;
        _removeEntryVisibility = false;
        //await AccountDataSynchronizationService.AccountChanged();
        await InvokeAsync(StateHasChanged);
    }

    public async Task ShowEditOverlay()
    {
        _updateEntryVisibility = true;
        await Task.CompletedTask;
    }
    public async Task ShowRemoveOverlay()
    {
        _removeEntryVisibility = true;
        await Task.CompletedTask;
    }

}