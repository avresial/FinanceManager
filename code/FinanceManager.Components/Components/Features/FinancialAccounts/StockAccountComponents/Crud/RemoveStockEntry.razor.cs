using FinanceManager.Application.Identity.Users;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Stock.Entities;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Features.FinancialAccounts.StockAccountComponents.Crud;

public partial class RemoveStockEntry
{
    private DateTime? _date = new DateTime();
    private decimal _balanceChange;
    private Currency _currency = DefaultCurrency.PLN;
    private string _isin = "ISIN";
    private string _investmentType = "InvestmentType";

    [Parameter] public EventCallback Confirm { get; set; }
    [Parameter] public EventCallback Cancel { get; set; }
    [Parameter] public required string StockAccountName { get; set; }
    [Parameter] public required StockAccountEntry StockEntry { get; set; }

    [Inject] public required ISettingsService SettingsService { get; set; }

    protected override void OnParametersSet()
    {
        _date = StockEntry.PostingDate;
        _balanceChange = StockEntry.ValueChange;
        _currency = SettingsService.GetCurrency();
        _isin = StockEntry.Isin;
        _investmentType = StockEntry.InvestmentType.ToString();
    }
}