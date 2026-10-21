using FinanceManager.Components.Features.FinancialAccounts.Components.Shared;
using FinanceManager.Components.Shared.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace FinanceManager.Components.Features.Dashboard.Components;

public partial class TransactionLogToolbar : ComponentBase
{
    /// <summary>Preset ranges offered by the dropdown: the same ones the account details pages use.</summary>
    public static readonly string[] Ranges = AccountHistoryToolbar.Ranges;

    private MudMenu? _rangeMenu;
    private MudDateRangePicker? _customDateRangePicker;

    [Parameter] public string SelectedRange { get; set; } = "3M";
    [Parameter] public EventCallback<string> SelectedRangeChanged { get; set; }
    [Parameter] public DateRange? CustomDateRange { get; set; }
    [Parameter] public EventCallback<DateRange?> CustomDateRangeChanged { get; set; }

    private bool IsCustomRange => SelectedRange == DateRangeHelper.CustomRangeKey;

    /// <summary>Label on the dropdown: the preset key, or the picked days for a custom range.</summary>
    private string RangeButtonText =>
        IsCustomRange && CustomDateRange is { Start: DateTime start, End: DateTime end }
            ? DateFormatter.FormatRange(start, end)
            : SelectedRange;

    private string RangeButtonAccessibleText =>
        IsCustomRange && CustomDateRange is { Start: DateTime start, End: DateTime end }
            ? $"Date range: {DateFormatter.Format(start)} to {DateFormatter.Format(end)}"
            : $"Date range: {SelectedRange}";

    private async Task ToggleRangeMenu(MouseEventArgs args)
    {
        if (_rangeMenu is not null)
            await _rangeMenu.ToggleMenuAsync(args);
    }

    private async Task OnRangeSelected(string value)
    {
        SelectedRange = value;
        if (_rangeMenu is not null)
            await _rangeMenu.CloseMenuAsync();
        await SelectedRangeChanged.InvokeAsync(value);
    }

    private async Task OpenCustomDateRangePicker()
    {
        if (_rangeMenu is not null)
            await _rangeMenu.CloseMenuAsync();
        if (_customDateRangePicker is not null)
            await _customDateRangePicker.OpenAsync();
    }

    private async Task OnCustomDateRangeChanged(DateRange? value)
    {
        CustomDateRange = value;
        SelectedRange = DateRangeHelper.CustomRangeKey;
        if (value is { Start: DateTime, End: DateTime } && _customDateRangePicker is not null)
            await _customDateRangePicker.CloseAsync(submit: true);
        await CustomDateRangeChanged.InvokeAsync(value);
    }
}