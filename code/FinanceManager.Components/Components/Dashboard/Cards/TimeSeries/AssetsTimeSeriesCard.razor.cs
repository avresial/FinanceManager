using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards.TimeSeries;

public partial class AssetsTimeSeriesCard
{
    private ApexChart<TimeSeriesModel>? _chart;
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

    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public DateTime StartDateTime { get; set; }

    [Inject] public required ILogger<AssetsTimeSeriesCard> Logger { get; set; }
    [Inject] public required IMoneyFlowService MoneyFlowService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null) return;

        ChartData.Clear();
        ChartData.AddRange(await GetData());

        _options.Tooltip = new Tooltip
        {
            Y = new TooltipY
            {
                Formatter = ChartHelper.GetCurrencyFormatter(SettingsService.GetCurrency())
            }
        };
    }
    protected override async Task OnParametersSetAsync()
    {
        ChartData.Clear();
        ChartData.AddRange(await GetData());

        if (_chart is not null) await _chart.UpdateSeriesAsync(true);
    }

    private async Task<List<TimeSeriesModel>> GetData()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null) return [];

        try
        {
            return await MoneyFlowService.GetAssetsTimeSeries(user.UserId, StartDateTime, DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting assets time series data");
        }

        return [];
    }
}