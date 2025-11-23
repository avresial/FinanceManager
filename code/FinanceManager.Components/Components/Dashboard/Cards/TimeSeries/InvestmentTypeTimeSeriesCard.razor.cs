using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
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
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<InvestmentTypeTimeSeriesCard> Logger { get; set; }
    [Inject] public required StockPriceHttpClient StockPriceHttpClient { get; set; }
    [Inject] public required AssetsHttpClient AssetsHttpClient { get; set; }


    protected override async Task OnInitializedAsync()
    {
        _options.Tooltip = new Tooltip
        {
            Y = new TooltipY
            {
                Formatter = ChartHelper.GetCurrencyFormatter(SettingsService.GetCurrency().ShortName)
            }
        };

        var user = await LoginService.GetLoggedUser();
        if (user is null) return;
        _priceTimeseries.Clear();

        _priceTimeseries.AddRange(await GetData());
    }

    protected override async Task OnParametersSetAsync()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null) return;
        _priceTimeseries.Clear();
        _priceTimeseries.AddRange(await GetData());
    }

    private async Task<List<TimeSeriesModel>> GetData()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null) return [];
        List<TimeSeriesModel> result = [];
        try
        {
            result = await AssetsHttpClient.GetAssetsTimeSeries(user.UserId, DefaultCurrency.PLN, StartDateTime, EndDateTime);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting income data");
        }
        return result;
    }
}