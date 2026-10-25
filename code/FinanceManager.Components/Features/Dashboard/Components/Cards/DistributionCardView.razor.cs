using ApexCharts;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Components;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using System.Globalization;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class DistributionCardView
{
    private const string _chartSize = "190";

    private string _view = DistributionViewToggle.TypeView;
    private List<DistributionSlice> _slices = [];
    private string _chartKey = string.Empty;
    // Highlighted row (from row or slice hover) and the slice selected through the chart API (row hover only).
    private int? _activeIndex;
    private int? _selectedIndex;
    private bool _otherExpanded;
    private ApexChart<DistributionSlice>? _chart;
    private ApexChartOptions<DistributionSlice> _chartOptions = new();

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter] public string? InfoText { get; set; }
    [Parameter] public string? InfoAriaLabel { get; set; }
    [Parameter] public string SeriesName { get; set; } = string.Empty;
    [Parameter] public string ErrorText { get; set; } = "Could not load distribution.";
    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public bool HasError { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public EventCallback OnRetry { get; set; }
    [Parameter] public string CurrencyShortName { get; set; } = "PLN";
    [Parameter] public List<NameValueResult> TypeData { get; set; } = [];
    [Parameter] public List<NameValueResult> AccountData { get; set; } = [];

    private bool IsAccountView => _view == DistributionViewToggle.AccountView;

    // A single category would render as a one-slice donut that carries no information, so the card shows only its summary row.
    private bool IsSingleCategory => _slices.Count == 1;

    private decimal Total => Math.Round(_slices.Sum(x => x.Value), 2);

    protected override void OnParametersSet() => BuildSlices();

    private Task OnViewChangedAsync(string view)
    {
        _view = view;
        BuildSlices();
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void BuildSlices()
    {
        _slices = DistributionSlices.Build(IsAccountView ? AccountData : TypeData);
        _activeIndex = null;

        // Recreate the chart whenever the slices, their colours or the currency change, instead of patching
        // a live chart: ApexCharts reads colours from the options only when it is created.
        var key = string.Join('|', _slices.Select(x => $"{x.Name}:{x.Value.ToString(CultureInfo.InvariantCulture)}:{x.Color}"))
            + $"|{CurrencyShortName}";
        if (key == _chartKey) return;

        _chartKey = key;
        _selectedIndex = null;
        _otherExpanded = false;
        _chartOptions = new ApexChartOptions<DistributionSlice>
        {
            Chart = new Chart
            {
                Toolbar = new Toolbar { Show = false },
                Background = "transparent",
            },
            Legend = new Legend { Show = false },
            DataLabels = new DataLabels { Enabled = false },
            Colors = [.. _slices.Select(x => x.Color)],
            // The default white slice borders look harsh on the dark theme.
            Stroke = new Stroke { Width = 0 },
            PlotOptions = new PlotOptions
            {
                Pie = new PlotOptionsPie
                {
                    ExpandOnClick = true,
                    Donut = new PlotOptionsDonut { Size = "68%", Labels = new DonutLabels { Show = false } },
                },
            },
            Tooltip = new Tooltip
            {
                Y = new TooltipY { Formatter = ChartHelper.GetCurrencyFormatter(CurrencyShortName) },
            },
        };
    }

    private string? LinkHref(DistributionSlice slice) =>
        IsAccountView && slice.AccountId is int accountId ? $"/AccountDetails/{accountId}" : null;

    private static string BarStyle(DistributionSlice slice) =>
        $"width:{slice.Percent.ToString("0.#", CultureInfo.InvariantCulture)}%;background:{slice.Color}";

    private void ToggleOther() => _otherExpanded = !_otherExpanded;

    // Hovering a row selects its slice, which pops the slice out of the donut; leaving deselects it again.
    private async Task OnRowEnterAsync(int index)
    {
        _activeIndex = index;
        if (_selectedIndex == index) return;
        if (_selectedIndex is int previous) await ToggleSliceAsync(previous);
        _selectedIndex = index;
        await ToggleSliceAsync(index);
    }

    private async Task OnRowLeaveAsync(int index)
    {
        if (_activeIndex == index) _activeIndex = null;
        if (_selectedIndex != index) return;
        _selectedIndex = null;
        await ToggleSliceAsync(index);
    }

    private async Task ToggleSliceAsync(int index)
    {
        if (_chart is not null && !IsSingleCategory)
            await _chart.ToggleDataPointSelectionAsync(0, index);
    }

    // Hovering a slice highlights its row only; the chart already shows its own hover state.
    private void OnSliceEnter(HoverData<DistributionSlice> hover)
    {
        if (hover.DataPointIndex < 0 || hover.DataPointIndex >= _slices.Count) return;
        _activeIndex = hover.DataPointIndex;
        StateHasChanged();
    }

    private void OnSliceLeave(HoverData<DistributionSlice> hover)
    {
        if (_activeIndex is null) return;
        _activeIndex = null;
        StateHasChanged();
    }
}