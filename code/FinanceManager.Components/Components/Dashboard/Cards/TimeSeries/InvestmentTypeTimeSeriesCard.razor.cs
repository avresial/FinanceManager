using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards.TimeSeries;

public partial class InvestmentTypeTimeSeriesCard
{
    private ApexChart<TimeSeriesModel>? _chart;
    private List<TimeSeriesModel> _priceTimeseries = [];
    private ApexChartOptions<TimeSeriesModel> _options { get; set; } = new()
    {
        Chart = new Chart
        {
            Sparkline = new ChartSparkline()
            {
                Enabled = true,
            },
            Toolbar = new Toolbar
            {
                Show = false
            },
        },
        Xaxis = new XAxis()
        {
            AxisTicks = new AxisTicks()
            {
                Show = false,
            },
            AxisBorder = new AxisBorder()
            {
                Show = false
            },
        },
        Yaxis = new List<YAxis>()
        {
            new YAxis
            {
                Show = false,
                SeriesName = "Vaue",
                DecimalsInFloat = 0,
            }
        },
        Colors = new List<string>
        {
           ColorsProvider.GetColors().First()
        }
    };

    [Parameter] public DateTime StartDateTime { get; set; }

    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<InvestmentTypeTimeSeriesCard> Logger { get; set; }
    [Inject] public required IStockRepository StockRepository { get; set; }
    [Inject] public required IMoneyFlowService MoneyFlowService { get; set; }


    protected override async Task OnInitializedAsync()
    {
        _options.Tooltip = new Tooltip
        {
            Y = new TooltipY
            {
                Formatter = ChartHelper.GetCurrencyFormatter(SettingsService.GetCurrency())
            }
        };

        var user = await LoginService.GetLoggedUser();
        if (user is null) return;
        _priceTimeseries = await MoneyFlowService.GetAssetsTimeSeries(user.UserId, StartDateTime, DateTime.UtcNow);
    }

    protected override async Task OnParametersSetAsync()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null) return;
        _priceTimeseries = await MoneyFlowService.GetAssetsTimeSeries(user.UserId, StartDateTime, DateTime.UtcNow);
    }
}