using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.StockAccountComponents.Crud;

public partial class RemoveStockEntry
{
    private DateTime? date = new DateTime();
    private decimal balanceChange;
    private Currency currency = DefaultCurrency.PLN;
    private string isin = "ISIN";
    private string investmentType = "InvestmentType";

    [Parameter] public EventCallback Confirm { get; set; }
    [Parameter] public EventCallback Cancel { get; set; }
    [Parameter] public required string StockAccountName { get; set; }
    [Parameter] public required StockAccountEntry StockEntry { get; set; }

    [Inject] public required ISettingsService SettingsService { get; set; }

    protected override void OnParametersSet()
    {
        date = StockEntry.PostingDate;
        balanceChange = StockEntry.ValueChange;
        currency = SettingsService.GetCurrency();
        isin = StockEntry.Isin;
        investmentType = StockEntry.InvestmentType.ToString();
    }
}
