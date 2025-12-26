using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.FinancialAccounts.BondAccountComponents;

public partial class BondAccountDetailsRow : ComponentBase
{
    [Parameter] public required BondAccountEntry BondAccountEntry { get; set; }
    [Parameter] public required BondAccount BondAccount { get; set; }
    [Parameter] public BondDetails? BondDetails { get; set; }

    [Inject] public required ISettingsService SettingsService { get; set; }

    private bool _expanded;
    private bool _updateEntryVisibility;
    private bool _removeEntryVisibility;
    private Currency _currency = DefaultCurrency.PLN;

    protected override void OnInitialized()
    {
        _currency = SettingsService.GetCurrency();
    }

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
    public void HideOverlay()
    {
        _updateEntryVisibility = false;
        _removeEntryVisibility = false;
        StateHasChanged();
    }
    public void Confirm()
    {
        // Logic to remove entry
        HideOverlay();
    }
}
