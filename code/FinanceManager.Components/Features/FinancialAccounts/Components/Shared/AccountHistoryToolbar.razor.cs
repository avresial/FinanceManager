using FinanceManager.Components.Shared.Helpers;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.Shared;

public partial class AccountHistoryToolbar : ComponentBase
{
    public enum TxFilter
    {
        All,
        Income,
        Expense
    }

    /// <summary>Preset ranges offered by the date-range dropdown, in menu order.</summary>
    public static readonly string[] Ranges = ["Month", "1M", "3M", "6M", "YTD"];

    // Must match the key DateRangeHelper.GetAccountDetailsRange resolves the custom
    // range with; a diverging literal here made custom ranges fall back to the default.
    public const string CustomRangeKey = DateRangeHelper.CustomRangeKey;

    private MudMenu? _labelMenu;
    private MudMenu? _rangeMenu;
    private MudDateRangePicker? _customDateRangePicker;
    private MudTextField<string>? _searchField;

    private bool _isSearchExpanded;
    private bool _focusSearchOnRender;

    [Parameter] public required int AccountId { get; set; }
    [Parameter] public string? SearchText { get; set; }
    [Parameter] public EventCallback<string?> SearchTextChanged { get; set; }
    [Parameter] public TxFilter? ActiveFilter { get; set; }
    [Parameter] public EventCallback<TxFilter?> ActiveFilterChanged { get; set; }
    [Parameter] public IEnumerable<string>? AvailableLabels { get; set; }
    [Parameter] public HashSet<string> SelectedLabels { get; set; } = new HashSet<string>();
    [Parameter] public EventCallback<HashSet<string>> SelectedLabelsChanged { get; set; }
    [Parameter] public string SelectedRange { get; set; } = "3M";
    [Parameter] public EventCallback<string> SelectedRangeChanged { get; set; }
    [Parameter] public DateRange? CustomDateRange { get; set; }
    [Parameter] public EventCallback<DateRange?> CustomDateRangeChanged { get; set; }
    [Parameter] public EventCallback OnAddEntry { get; set; }
    [Parameter] public bool ShowInsightsButton { get; set; }
    [Parameter] public EventCallback OnInsightsClick { get; set; }
    [Parameter] public bool IsMobile { get; set; }

    private string _searchPanelId = string.Empty;

    private string CategoryButtonText => SelectedLabels.Count switch
    {
        0 => "Filter by category",
        1 => $"Category: {SelectedLabels.First()}",
        _ => $"{SelectedLabels.Count} categories"
    };

    /// <summary>
    /// Label on the date-range dropdown: the preset key, or the picked days for a custom range
    /// so the button still says which window is on screen.
    /// </summary>
    private string RangeButtonText
    {
        get
        {
            if (SelectedRange != CustomRangeKey) return SelectedRange;
            if (CustomDateRange is not { Start: DateTime start, End: DateTime end }) return "Custom";

            // A dated label is ~45px wider than the phone row can spare once the type filter,
            // category, search and add controls have taken their share, so mobile keeps the
            // short form — the picked days are still shown in the picker and the change card.
            return IsMobile ? "Custom" : $"{start:dd/MM} – {end:dd/MM}";
        }
    }

    protected override void OnInitialized()
    {
        _searchPanelId = $"fm-tx-search-{AccountId}";

        // A search term restored from a previous visit would otherwise filter the list
        // with nothing on screen explaining why.
        _isSearchExpanded = !string.IsNullOrWhiteSpace(SearchText);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_focusSearchOnRender) return;

        _focusSearchOnRender = false;
        if (_searchField is not null)
            await _searchField.FocusAsync();
    }

    private void ToggleSearch()
    {
        _isSearchExpanded = !_isSearchExpanded;
        _focusSearchOnRender = _isSearchExpanded;
    }

    private void OnSearchKeyDown(KeyboardEventArgs args)
    {
        if (args.Key == "Escape")
            _isSearchExpanded = false;
    }

    private async Task OnSearchChanged(string? value)
    {
        SearchText = value;
        await SearchTextChanged.InvokeAsync(value);
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

    private async Task ToggleLabelMenu(MouseEventArgs args)
    {
        if (_labelMenu is not null)
            await _labelMenu.ToggleMenuAsync(args);
    }

    private async Task CloseLabelMenu()
    {
        if (_labelMenu is not null)
            await _labelMenu.CloseMenuAsync();
    }

    private async Task ToggleRangeMenu(MouseEventArgs args)
    {
        if (_rangeMenu is not null)
            await _rangeMenu.ToggleMenuAsync(args);
    }

    private async Task OnRangeSelected(string value)
    {
        SelectedRange = value;
        await SelectedRangeChanged.InvokeAsync(value);
    }

    private Task OpenCustomDateRangePicker() =>
        _customDateRangePicker?.OpenAsync() ?? Task.CompletedTask;

    private async Task OnCustomDateRangeChanged(DateRange? value)
    {
        CustomDateRange = value;
        SelectedRange = CustomRangeKey;
        await CustomDateRangeChanged.InvokeAsync(value);
    }
}