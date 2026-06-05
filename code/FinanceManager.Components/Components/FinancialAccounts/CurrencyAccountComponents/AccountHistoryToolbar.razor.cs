using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Components.FinancialAccounts.CurrencyAccountComponents;

public partial class AccountHistoryToolbar : ComponentBase
{
    public enum TxFilter
    {
        Income,
        Expense
    }

    private MudMenu? _labelMenu;

    [Parameter] public required int AccountId { get; set; }
    [Parameter] public string? SearchText { get; set; }
    [Parameter] public EventCallback<string?> SearchTextChanged { get; set; }
    [Parameter] public TxFilter? ActiveFilter { get; set; }
    [Parameter] public EventCallback<TxFilter?> ActiveFilterChanged { get; set; }
    [Parameter] public IEnumerable<string>? AvailableLabels { get; set; }
    [Parameter] public HashSet<string> SelectedLabels { get; set; } = new HashSet<string>();
    [Parameter] public EventCallback<HashSet<string>> SelectedLabelsChanged { get; set; }
    [Parameter] public EventCallback OnAddEntry { get; set; }
    [Parameter] public bool ShowInsightsButton { get; set; }
    [Parameter] public EventCallback OnInsightsClick { get; set; }
    [Parameter] public bool IsMobile { get; set; }

    private string CategoryButtonText => SelectedLabels.Count switch
    {
        0 => "Category",
        1 => SelectedLabels.First(),
        _ => $"{SelectedLabels.Count} categories"
    };

    private async Task OnSearchChanged(string? value)
    {
        SearchText = value;
        await SearchTextChanged.InvokeAsync(value);
    }

    private async Task ToggleFilter(TxFilter filter)
    {
        TxFilter? next = ActiveFilter == filter ? null : filter;
        ActiveFilter = next;
        await ActiveFilterChanged.InvokeAsync(next);
    }

    private async Task ToggleLabel(string label)
    {
        if (!SelectedLabels.Add(label))
            SelectedLabels.Remove(label);
        await SelectedLabelsChanged.InvokeAsync(SelectedLabels);
    }

    private async Task ClearLabels()
    {
        SelectedLabels = [];
        await SelectedLabelsChanged.InvokeAsync(SelectedLabels);
    }

    private async Task CloseLabelMenu()
    {
        if (_labelMenu is not null)
            await _labelMenu.CloseMenuAsync();
    }
}
