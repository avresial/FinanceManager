using ApexCharts;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Components.FinancialAccounts.Shared;

public partial class AccountDetailsHero
{
    [Parameter] public required string AccountName { get; set; }
    [Parameter] public string AccountTypeLabel { get; set; } = "Cash account";
    [Parameter] public required string Currency { get; set; }
    [Parameter] public decimal Balance { get; set; }
    [Parameter] public decimal BalanceChange { get; set; }
    [Parameter] public decimal? BalanceChangePercent { get; set; }
    [Parameter] public string SelectedRange { get; set; } = "3M";
    [Parameter] public EventCallback<string> SelectedRangeChanged { get; set; }
    [Parameter] public bool IsChartLoading { get; set; }
    [Parameter] public List<TimeSeriesModel> ChartData { get; set; } = [];
    [Parameter] public bool IsMobile { get; set; }

    private readonly string[] _ranges = ["1W", "1M", "3M", "6M", "YTD", "All"];

    private static readonly ApexChartOptions<TimeSeriesModel> _heroChartOptions = new()
    {
        Chart = new Chart
        {
            Background = "transparent",
            Toolbar = new Toolbar { Show = false },
            Animations = new Animations { Enabled = false },
        },
        Colors = ["#ffab00"],
        Stroke = new Stroke { Curve = Curve.Smooth, Width = 2, LineCap = LineCap.Round },
        DataLabels = new DataLabels { Enabled = false },
        Legend = new Legend { Show = false },
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
}
