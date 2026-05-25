using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.FinancialAccounts.StockAccountComponents;

public partial class StockAccountDetailsRow
{
    private decimal? _price = null;
    private decimal? _priceChange = null;
    private bool _expanded = false;
    private bool _removeEntryVisibility;
    private bool _updateEntryVisibility;
    internal Currency _currency = DefaultCurrency.PLN;

    [Parameter] public required StockAccount InvestmentAccount { get; set; }
    [Parameter] public required StockAccountEntry InvestmentEntry { get; set; }
    [Parameter] public StockPrice? ResolvedPrice { get; set; }
    [Parameter] public UnrealizedGainLossInstrumentResult? UnrealizedGainLoss { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }

    protected override void OnParametersSet()
    {
        _currency = SettingsService.GetCurrency();

        if (ResolvedPrice is null)
        {
            _price = null;
            _priceChange = null;
            base.OnParametersSet();
            return;
        }

        _currency = ResolvedPrice.Currency;
        _price = ResolvedPrice.PricePerUnit * InvestmentEntry.Value;
        _priceChange = ResolvedPrice.PricePerUnit * InvestmentEntry.ValueChange;

        base.OnParametersSet();
    }

    public async Task Confirm()
    {
        _updateEntryVisibility = false;
        _removeEntryVisibility = false;
        _expanded = false;

        InvestmentAccount.Remove(InvestmentEntry.EntryId);
        await FinancialAccountService.RemoveEntry(InvestmentEntry.EntryId, InvestmentAccount.AccountId);
        await InvokeAsync(StateHasChanged);
    }

    public async Task HideOverlay()
    {
        _updateEntryVisibility = false;
        _removeEntryVisibility = false;
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