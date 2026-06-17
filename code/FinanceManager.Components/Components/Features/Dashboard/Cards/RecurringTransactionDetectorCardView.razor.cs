using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Labels.Entities;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards;

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
}