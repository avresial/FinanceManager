using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class RecurringTransactionDetectorCardView
{
    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool IsLoadingDetail { get; set; }
    [Parameter] public List<RecurringTransactionResult> Data { get; set; } = [];
    [Parameter] public decimal TotalMonthlySpend { get; set; }
    [Parameter] public string Currency { get; set; } = "PLN";
    [Parameter] public RecurringTransactionResult? SelectedItem { get; set; }
    [Parameter] public List<CurrencyAccountEntry> DetailEntries { get; set; } = [];
    [Parameter] public EventCallback<RecurringTransactionResult> OnItemSelected { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }

    private double GetPercentage(decimal value)
    {
        if (Data.Count == 0) return 0;
        var max = Data.Max(x => x.Value);
        return max == 0 ? 0 : (double)(value / max * 100);
    }

    // Income carries an explicit "+", spend stays unsigned: the card already colours the direction.
    private string FormatFlow(bool isIncome, decimal value) => isIncome
        ? MoneyFormatter.FormatSigned(Math.Abs(value), Currency)
        : MoneyFormatter.Format(Math.Abs(value), Currency);
}