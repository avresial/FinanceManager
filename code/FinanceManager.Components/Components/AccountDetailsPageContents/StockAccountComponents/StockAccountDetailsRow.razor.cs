using FinanceManager.Components.HttpContexts;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.Accounts;
using FinanceManager.Domain.Entities.Accounts.Entries;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.AccountDetailsPageContents.StockAccountComponents;
public partial class StockAccountDetailsRow
{
    private decimal? _price = null;
    private bool _expanded = false;
    private bool RemoveEntryVisibility;
    private bool UpdateEntryVisibility;
    internal Currency currency = DefaultCurrency.PLN;

    [Parameter] public required StockAccount InvestmentAccount { get; set; }
    [Parameter] public required StockAccountEntry InvestmentEntry { get; set; }

    [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required StockPriceHttpContext stockPriceHttpContext { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        try
        {
            var price = await stockPriceHttpContext.GetStockPrice(InvestmentEntry.Ticker, currency, InvestmentEntry.PostingDate);
            if (price is null)
            {
                _price = null;
            }
            else
            {
                currency = price.Currency;
                _price = price.PricePerUnit * InvestmentEntry.Value;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            _price = null;
        }
    }

    protected override void OnParametersSet()
    {
        currency = SettingsService.GetCurrency();

        base.OnParametersSet();
    }

    public async Task Confirm()
    {
        UpdateEntryVisibility = false;
        RemoveEntryVisibility = false;
        _expanded = false;
        await InvokeAsync(StateHasChanged);

        InvestmentAccount.Remove(InvestmentEntry.EntryId);
        await FinancialAccountService.RemoveEntry(InvestmentEntry.EntryId, InvestmentAccount.AccountId);
    }

    public async Task Cancel()
    {
        UpdateEntryVisibility = false;
        RemoveEntryVisibility = false;
        await InvokeAsync(StateHasChanged);
    }

    public async Task HideOverlay()
    {
        UpdateEntryVisibility = false;
        RemoveEntryVisibility = false;
        await InvokeAsync(StateHasChanged);
    }

    public async Task ShowEditOverlay()
    {
        UpdateEntryVisibility = true;
        await Task.CompletedTask;
    }
    public async Task ShowRemoveOverlay()
    {
        RemoveEntryVisibility = true;
        await Task.CompletedTask;
    }

}