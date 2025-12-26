using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.FinancialAccounts.BondAccountComponents;

public partial class BondAccountDetailsRow : ComponentBase
{
    private bool _expanded;
    private bool _updateEntryVisibility;
    private bool _removeEntryVisibility;
    private Currency _currency = DefaultCurrency.PLN;

    [Parameter] public required BondAccountEntry BondAccountEntry { get; set; }
    [Parameter] public required BondAccount BondAccount { get; set; }
    [Parameter] public BondDetails? BondDetails { get; set; }

    [Inject] public required AccountDataSynchronizationService AccountDataSynchronizationService { get; set; }
    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILogger<BondAccountDetailsRow> Logger { get; set; }

    protected override void OnInitialized() => _currency = SettingsService.GetCurrency();

    public void ShowEditOverlay()
    {
        _updateEntryVisibility = true;
        StateHasChanged();
    }
    public void ShowRemoveOverlay()
    {
        _removeEntryVisibility = true;
        StateHasChanged();
    }
    public async Task HideOverlay()
    {
        _updateEntryVisibility = false;
        _removeEntryVisibility = false;
        await InvokeAsync(StateHasChanged);
    }
    public async Task Confirm()
    {
        try
        {
            await FinancialAccountService.RemoveEntry(BondAccountEntry.EntryId, BondAccount.AccountId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while removing entry");
        }

        await AccountDataSynchronizationService.AccountChanged();
        await HideOverlay();

        await InvokeAsync(StateHasChanged);
    }
}
