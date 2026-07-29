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

    private MudDateRangePicker? _customDateRangePicker;

    private Task OpenCustomDateRangePicker() =>
        _customDateRangePicker?.OpenAsync() ?? Task.CompletedTask;

    private static readonly ApexChartOptions<TimeSeriesModel> _heroChartOptions = new()
    {
        Chart = new Chart
        {
            Background = "transparent",
            Toolbar = new Toolbar { Show = false },
            Animations = new Animations { Enabled = false },
        },
        Colors = ["#ffab00", "#42a5f5"],
        Stroke = new Stroke { Curve = Curve.Smooth, Width = 2, LineCap = LineCap.Round },
        DataLabels = new DataLabels { Enabled = false },
        Legend = new Legend { Show = true, ShowForSingleSeries = false },
        Fill = new Fill
        {
            Type = [FillType.Gradient],
            Gradient = new FillGradient { OpacityFrom = 0.45, OpacityTo = 0d, Stops = [0, 100] },
        },
        Grid = new Grid { Show = false, Padding = new Padding { Left = 0, Right = 0, Top = 0, Bottom = 0 } },
        Xaxis = new XAxis
        {
            Labels = new XAxisLabels { Show = false },
            AxisBorder = new AxisBorder { Show = false },
            AxisTicks = new AxisTicks { Show = false },
        },
        Yaxis = [new YAxis { Show = false, Labels = new YAxisLabels { Show = false } }],
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