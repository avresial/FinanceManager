using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.Dashboard.Dtos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace FinanceManager.Components.Features.Dashboard.Components;

public partial class TransactionLogToolbar : ComponentBase
{
    /// <summary>Preset ranges offered by the date-range dropdown, in menu order.</summary>
    public static readonly string[] Ranges = ["Month", "1M", "3M", "6M", "YTD"];

    private static readonly (TransactionKind Type, string Label)[] _typeOptions =
    [
        (TransactionKind.Cash, "Cash"),
        (TransactionKind.Bond, "Bonds"),
        (TransactionKind.Investment, "Investments"),
    ];

    private readonly string _searchPanelId = $"fm-txlog-search-{Guid.NewGuid():N}";
    private MudTextField<string>? _searchField;
    private MudIconButton? _searchButton;
    private bool _isSearchExpanded;
    private bool _focusSearchOnRender;
    private bool _focusButtonOnRender;

    [Parameter] public string? SearchText { get; set; }
    [Parameter] public EventCallback<string?> SearchTextChanged { get; set; }

    protected override void OnInitialized() => _isSearchExpanded = !string.IsNullOrWhiteSpace(SearchText);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_focusSearchOnRender && _searchField is not null)
        {
            _focusSearchOnRender = false;
            await _searchField.FocusAsync();
        }
        if (_focusButtonOnRender && _searchButton is not null)
        {
            _focusButtonOnRender = false;
            await _searchButton.FocusAsync();
        }
    }

    private void ToggleSearch()
    {
        _isSearchExpanded = !_isSearchExpanded;
        _focusSearchOnRender = _isSearchExpanded;
    }

    private void OnSearchKeyDown(KeyboardEventArgs args)
    {
        if (args.Key != "Escape") return;
        _isSearchExpanded = false;
        _focusButtonOnRender = true;
    }

    private async Task OnSearchChanged(string? value)
    {
        SearchText = value;
        await SearchTextChanged.InvokeAsync(value);
    }

    private MudMenu? _rangeMenu;
    private MudDateRangePicker? _customDateRangePicker;

    [Parameter] public string SelectedRange { get; set; } = "3M";
    [Parameter] public EventCallback<string> SelectedRangeChanged { get; set; }
    [Parameter] public DateRange? CustomDateRange { get; set; }
    [Parameter] public EventCallback<DateRange?> CustomDateRangeChanged { get; set; }

    [Parameter] public IReadOnlyList<string> Currencies { get; set; } = [];
    [Parameter] public TransactionLogFilter Filter { get; set; } = TransactionLogFilter.None;
    [Parameter] public EventCallback<TransactionLogFilter> FilterChanged { get; set; }

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

    private Task OnKindsChanged(IReadOnlyCollection<TransactionKind>? selected) =>
        FilterChanged.InvokeAsync(Filter with { Kinds = [.. (selected ?? []).Distinct().Order()] });

    private Task OnCurrenciesChanged(IEnumerable<string>? selected) =>
        FilterChanged.InvokeAsync(Filter with { Currencies = [.. (selected ?? []).Distinct()] });
}