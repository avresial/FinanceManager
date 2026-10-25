using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.Labels.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class RecurringTransactionDetectorCardView
{
    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool IsLoadingDetail { get; set; }
    [Parameter] public bool HasError { get; set; }
    [Parameter] public bool HasDetailError { get; set; }
    [Parameter] public EventCallback OnRetry { get; set; }
    [Parameter] public EventCallback OnRetryDetail { get; set; }
    [Parameter] public List<RecurringTransactionResult> Data { get; set; } = [];
    [Parameter] public decimal TotalMonthlySpend { get; set; }
    [Parameter] public string Currency { get; set; } = "PLN";
    [Parameter] public RecurringTransactionResult? SelectedItem { get; set; }
    [Parameter] public List<CurrencyAccountEntry> DetailEntries { get; set; } = [];
    [Parameter] public EventCallback<RecurringTransactionResult> OnItemSelected { get; set; }
    [Parameter] public EventCallback OnBack { get; set; }

    private List<RecurringTransactionResult> _income = [];
    private List<RecurringTransactionResult> _outgoing = [];
    private readonly Dictionary<RecurringTransactionResult, MudButton> _rowButtons = [];
    private MudIconButton? _backButton;
    private RecurringTransactionResult? _previousSelection;
    private RecurringTransactionResult? _returnFocusTo;
    private bool _selectionChanged;
    private decimal TotalMonthlyIncome => _income.Sum(item => Math.Abs(item.Value));
    private decimal TotalMonthlyOutgoing => _outgoing.Sum(item => Math.Abs(item.Value));
    private decimal NetMonthlyFlow => TotalMonthlyIncome - TotalMonthlyOutgoing;

    protected override void OnParametersSet()
    {
        _selectionChanged = !ReferenceEquals(_previousSelection, SelectedItem);
        _returnFocusTo = _previousSelection;
        _previousSelection = SelectedItem;
        _rowButtons.Clear();
        _income = [.. Data.Where(item => item.IsIncome).OrderByDescending(item => Math.Abs(item.Value))];
        _outgoing = [.. Data.Where(item => !item.IsIncome).OrderByDescending(item => Math.Abs(item.Value))];
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_selectionChanged) return;
        _selectionChanged = false;

        if (SelectedItem is not null && _backButton is not null)
        {
            await _backButton.FocusAsync();
        }
        else if (_returnFocusTo is not null && _rowButtons.TryGetValue(_returnFocusTo, out var row))
        {
            await row.FocusAsync();
        }
    }

    private static double GetPercentage(decimal value, List<RecurringTransactionResult> group)
    {
        var max = Math.Abs(group[0].Value);
        return max == 0 ? 0 : (double)(Math.Abs(value) / max * 100);
    }

    private string FormatFlow(bool isIncome, decimal value) =>
        MoneyFormatter.FormatSigned(isIncome ? Math.Abs(value) : -Math.Abs(value), Currency);
}