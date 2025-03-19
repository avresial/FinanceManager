using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class InvestmentTypeTimeSeriesCard
    {

        private ApexChart<TimeSeriesModel>? chart;
        List<TimeSeriesModel> priceTimeseries = [];

        [Parameter]
        public DateTime StartDateTime { get; set; }

        [Inject]
        public required ISettingsService settingsService { get; set; }

        [Inject]
        public required ILoginService loginService { get; set; }

        [Inject]
        public required ILogger<InvestmentTypeTimeSeriesCard> Logger { get; set; }

        [Inject]
        public required IStockRepository StockRepository { get; set; }

        [Inject]
        public required IMoneyFlowService MoneyFlowService { get; set; }


        protected override async Task OnInitializedAsync()
        {
            options.Tooltip = new ApexCharts.Tooltip
            {
                Y = new TooltipY
                {
                    Formatter = ChartHelper.GetCurrencyFormatter(settingsService.GetCurrency())
                }
            };

            var user = await loginService.GetLoggedUser();
            if (user is null) return;
            priceTimeseries = await MoneyFlowService.GetAssetsTimeSeries(user.UserId, StartDateTime, DateTime.UtcNow);
        }

        protected override async Task OnParametersSetAsync()
        {
            var user = await loginService.GetLoggedUser();
            if (user is null) return;
            priceTimeseries = await MoneyFlowService.GetAssetsTimeSeries(user.UserId, StartDateTime, DateTime.UtcNow);
        }

        private ApexChartOptions<TimeSeriesModel> options { get; set; } = new()
        {
            Chart = new Chart
            {
                Sparkline = new ChartSparkline()
                {
                    Enabled = true,
                },
                Toolbar = new ApexCharts.Toolbar
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
    }
}