using ApexCharts;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace FinanceManager.Components.Features.FinancialAccounts.Components.Shared;

public partial class AccountDetailsHero
{
    [Parameter] public required string AccountName { get; set; }
    [Parameter] public string AccountTypeLabel { get; set; } = "Cash account";
    [Parameter] public required string Currency { get; set; }
    [Parameter] public decimal Balance { get; set; }
    [Parameter] public decimal BalanceChange { get; set; }
    [Parameter] public decimal? BalanceChangePercent { get; set; }
    [Parameter] public string ChangeLabel { get; set; } = "Change";
    [Parameter] public bool ShowChangeRange { get; set; } = true;
    [Parameter] public string SelectedRange { get; set; } = "3M";
    [Parameter] public EventCallback<string> SelectedRangeChanged { get; set; }
    [Parameter] public DateRange? CustomDateRange { get; set; }
    [Parameter] public EventCallback<DateRange?> CustomDateRangeChanged { get; set; }
    [Parameter] public bool IsChartLoading { get; set; }

    // Some account types (investments) only know their balance/appreciation after an async
    // valuation call that resolves after the transaction list has already rendered. While that
    // is in flight this shows a skeleton in place of a misleading 0.00 figure. Left false by
    // callers whose balance is known synchronously.
    [Parameter] public bool IsBalanceLoading { get; set; }
    [Parameter] public List<TimeSeriesModel> ChartData { get; set; } = [];
    [Parameter] public List<TimeSeriesModel> BenchmarkData { get; set; } = [];
    [Parameter] public string BenchmarkName { get; set; } = "Benchmark";
    [Parameter] public bool IsMobile { get; set; }

    // Must match the key DateRangeHelper.GetAccountDetailsRange resolves the custom
    // range with; a diverging literal here made custom ranges fall back to the default.
    public const string CustomRangeKey = DateRangeHelper.CustomRangeKey;

    private readonly string[] _ranges = ["Month", "1M", "3M", "6M", "YTD"];

    // Floating y-axis labels are right-aligned at this offset from the plot's left edge, so
    // the value has to clear the widest label rather than the narrowest: the dashboard's 24
    // suits "31k" but leaves a decimal tick like "301.03" flush against the page edge, and
    // the hero chart is full-bleed (mx-n6) so there is no card padding to fall back on.
    private const double _floatingYLabelOffsetX = 48;

    private const int _yAxisTickCount = 5;

    /// <summary>Legend/series name for the account's own balance series.</summary>
    public const string BalanceSeriesName = "Balance";

    private MudDateRangePicker? _customDateRangePicker;

    private Task OpenCustomDateRangePicker() =>
        _customDateRangePicker?.OpenAsync() ?? Task.CompletedTask;

    // The y-axis is scaled to the series actually on screen (see ApplyYScale), so the options
    // object is per-instance rather than a shared static one.
    private readonly ApexChartOptions<TimeSeriesModel> _heroChartOptions = BuildChartOptions();

    // ApexCharts only paints through JS interop when the ApexChart component is first
    // rendered; pushing new Items into an already-rendered chart changes nothing on screen.
    // The hero repaints a cached snapshot and then applies the refreshed series without
    // toggling IsChartLoading in between, so that second update would otherwise be dropped.
    // Track which instance drew which points and ask the live chart to redraw when they change.
    private ApexChart<TimeSeriesModel>? _chart;
    private ApexChart<TimeSeriesModel>? _drawnChart;
    private List<(string Series, DateTime DateTime, decimal Value)> _drawnPoints = [];

    protected override void OnParametersSet() => ApplyYScale();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (IsChartLoading || _chart is null) return;

        // Each point carries its series name so the comparison notices a moved boundary
        // between the two series (a flat concatenation of the same points would compare
        // equal) and a renamed benchmark, which changes the legend without changing values.
        var points = ChartData
            .Select(p => (BalanceSeriesName, p.DateTime, p.Value))
            .Concat(BenchmarkData.Select(p => (BenchmarkName, p.DateTime, p.Value)));
        if (!ReferenceEquals(_chart, _drawnChart))
        {
            // A freshly mounted chart instance (first render, or remounted after the loading
            // skeleton) has just drawn the current data itself.
            _drawnChart = _chart;
            _drawnPoints = [.. points];
            return;
        }

        if (points.SequenceEqual(_drawnPoints)) return;

        _drawnPoints = [.. points];
        await _chart.RenderAsync();
    }

    /// <summary>
    /// Keeps small balance movements visible by padding the displayed range instead of
    /// forcing the series through zero — the same scaling the dashboard time-series cards use.
    /// </summary>
    private void ApplyYScale()
    {
        var axis = _heroChartOptions.Yaxis?.FirstOrDefault();
        if (axis is null) return;

        // The benchmark line is rebased onto the portfolio, so both series share one axis and
        // the padded bounds have to span them together or the rebased line clips.
        var values = ChartData.Concat(BenchmarkData).Select(p => (double)p.Value).ToList();
        if (values.Count == 0) return;

        var bounds = ChartHelper.AddYRangePadding(values.Min(), values.Max());
        axis.Min = bounds.Min;
        axis.Max = bounds.Max;
        axis.TickAmount = _yAxisTickCount;
        axis.ForceNiceScale = false;

        // Account balances span a much wider magnitude range than the dashboard's portfolio
        // aggregates (a 300 PLN bond sits beside a 30k cash account), so the tick precision
        // is chosen from the scale actually on screen rather than fixed to "k".
        if (axis.Labels is YAxisLabels labels)
            labels.Formatter = ChartHelper.GetYAxisTickFormatter(bounds.Min, bounds.Max, _yAxisTickCount);
    }

    private static ApexChartOptions<TimeSeriesModel> BuildChartOptions() => new()
    {
        Chart = new Chart
        {
            Background = "transparent",
            Toolbar = new Toolbar { Show = false },
            Zoom = new Zoom { Enabled = false },
            FontFamily = "Roboto, sans-serif",
            // Same draw animation as the dashboard time-series cards.
            Animations = new Animations { Enabled = true, Speed = 400 },
        },
        Colors = ["#ffab00", "#42a5f5"],
        Stroke = new Stroke { Curve = Curve.Smooth, Width = 2, LineCap = LineCap.Round },
        DataLabels = new DataLabels { Enabled = false },
        // Top-aligned so the legend does not compete with the x-axis label row below the plot.
        Legend = new Legend
        {
            Show = true,
            ShowForSingleSeries = false,
            Position = LegendPosition.Top,
            HorizontalAlign = ApexCharts.Align.Right,
        },
        Fill = new Fill
        {
            Type = [FillType.Gradient],
            Gradient = new FillGradient { OpacityFrom = 0.45, OpacityTo = 0d, Stops = [0, 100] },
        },
        Grid = new Grid
        {
            Show = true,
            // Theme-neutral grey: the app follows the system light/dark preference, so
            // gridlines and labels must read on both a light and a dark surface.
            BorderColor = "rgba(128,128,128,0.18)",
            StrokeDashArray = 0,
            Padding = new Padding { Left = 0, Right = 0, Top = 4, Bottom = 0 },
            Xaxis = new GridXAxis { Lines = new Lines { Show = false } },
            Yaxis = new GridYAxis { Lines = new Lines { Show = true } },
        },
        Xaxis = new XAxis
        {
            Type = XAxisType.Datetime,
            // No fixed TickAmount: ApexCharts places datetime ticks on natural boundaries. A
            // fixed count lands ticks mid-month and repeats labels when the series has fewer
            // points than ticks.
            AxisBorder = new AxisBorder { Show = false },
            AxisTicks = new AxisTicks { Show = false },
            Crosshairs = new AxisCrosshairs { Show = true, Width = 1 },
            Labels = new XAxisLabels
            {
                Style = new AxisLabelStyle { Colors = "rgba(130,130,130,0.95)", FontSize = "11px" },
                // Format per tick granularity so sub-month ranges do not repeat the month
                // (a daily "Month" range would otherwise show "Jun '26" on every tick).
                DatetimeFormatter = new DatetimeFormatter { Year = "yyyy", Month = "MMM 'yy", Day = "dd MMM" },
            },
        },
        Yaxis =
        [
            new YAxis
            {
                Show = true,
                // Min / Max / TickAmount are set per-series in ApplyYScale.
                // Floating = true: labels overlay the plot instead of occupying a reserved
                // column, so the full-bleed chart keeps its edge-to-edge width.
                Floating = true,
                AxisBorder = new AxisBorder { Show = false },
                AxisTicks = new AxisTicks { Show = false },
                Labels = new YAxisLabels
                {
                    Show = true,
                    // Nudges the floating labels right off the left chart edge so they sit
                    // just inside the visible area rather than being cut by it.
                    OffsetX = _floatingYLabelOffsetX,
                    Style = new AxisLabelStyle { Colors = "rgba(130,130,130,0.95)", FontSize = "11px" },
                    Formatter = ChartHelper.CompactCurrencyTickFormatter,
                },
            },
        ],
        Tooltip = new Tooltip { Enabled = false },
    };

    private string GetInitial() => string.IsNullOrWhiteSpace(AccountName) ? "?" : AccountName.Trim()[..1].ToUpperInvariant();

    private async Task OnRangeChanged(string value)
    {
        SelectedRange = value;
        await SelectedRangeChanged.InvokeAsync(value);
    }

    private async Task OnCustomDateRangeChanged(DateRange? value)
    {
        CustomDateRange = value;
        SelectedRange = CustomRangeKey;
        await CustomDateRangeChanged.InvokeAsync(value);
    }
}