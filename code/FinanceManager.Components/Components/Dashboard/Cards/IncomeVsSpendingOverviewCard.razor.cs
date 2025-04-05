using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class IncomeVsSpendingOverviewCard
{
    private UserSession? _user;

    private ApexChart<IncomeVsSpendingEntry>? _chart;

    private ApexChartOptions<IncomeVsSpendingEntry> _options = new()
    {
        Colors = new List<string> { "#00FF00", "#FF0000" },
        Fill = new Fill
        {
            Type = new List<FillType> { FillType.Gradient, FillType.Gradient },
            Gradient = new FillGradient
            {
                ShadeIntensity = 1,
                OpacityFrom = 0.2,
                OpacityTo = 0.9,

            },
        },
    };

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }

    [Inject] public required IMoneyFlowService MoneyFlowService { get; set; }
    [Inject] public required ILogger<IncomeVsSpendingOverviewCard> Logger { get; set; }

    public List<IncomeVsSpendingEntry> ChartData { get; set; } = [];

    protected override void OnInitialized()
    {
        _options.Tooltip = new ApexCharts.Tooltip
        {
            Y = new TooltipY
            {
                Formatter = ChartHelper.GetCurrencyFormatter(settingsService.GetCurrency())
            }
        };
        _options.Chart = new Chart
        {
            Toolbar = new ApexCharts.Toolbar
            {
                Show = false
            },
            Zoom = new Zoom()
            {
                Enabled = false
            },
            Sparkline = new ChartSparkline()
            {
                Enabled = true,
            },
        };


        _options.Xaxis = new XAxis()
        {
            AxisTicks = new AxisTicks()
            {
                Show = false,
            },
            AxisBorder = new AxisBorder()
            {
                Show = false
            },
            Position = XAxisPosition.Bottom,
            Type = XAxisType.Datetime

        };

        _options.Yaxis = new List<YAxis>();

        _options.Yaxis.Add(new YAxis
        {
            AxisTicks = new AxisTicks()
            {
                Show = false
            },
            Show = false,
            SeriesName = "NetValue",
            DecimalsInFloat = 0,

        });
        _options.Colors = new List<string>
        {
            "#B2BF84",
            "#D93D3D",
        };
    }


    protected override async Task OnParametersSetAsync()
    {
        _user = await loginService.GetLoggedUser();
        if (_user is null) return;

        ChartData.Clear();
        ChartData.AddRange(await GetData());

        if (_chart is not null) await _chart.UpdateSeriesAsync(true);
    }

    private async Task<List<IncomeVsSpendingEntry>> GetData()
    {
        List<IncomeVsSpendingEntry> result = [];
        TimeSpan timeSeriesStep = new TimeSpan(1, 0, 0, 0);
        DateTime end = DateTime.UtcNow;

        var income = await MoneyFlowService.GetIncome(_user.UserId, StartDateTime, end, timeSeriesStep);
        var spending = await MoneyFlowService.GetSpending(_user.UserId, StartDateTime, end, timeSeriesStep);

        for (var date = end; date >= StartDateTime; date = date.Add(-timeSeriesStep))
        {
            result.Add(new IncomeVsSpendingEntry
            {
                Date = date,
                Income = income.FirstOrDefault(x => x.DateTime == date)?.Value ?? 0,
                Spending = spending.FirstOrDefault(x => x.DateTime == date)?.Value ?? 0
            });
        }

        return result;
    }
}
public class IncomeVsSpendingEntry
{
    public DateTime Date;
    public decimal Income;
    public decimal Spending;
}