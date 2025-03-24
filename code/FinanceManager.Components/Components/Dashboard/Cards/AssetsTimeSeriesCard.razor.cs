using ApexCharts;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class AssetsTimeSeriesCard
    {

        private ApexChart<TimeSeriesModel>? chart;
        private List<TimeSeriesModel> priceTimeseries = new List<TimeSeriesModel>();

        [Parameter]
        public DateTime StartDateTime { get; set; }

        [Inject]
        public required ILogger<AssetsTimeSeriesCard> Logger { get; set; }

        [Inject]
        public required IMoneyFlowService MoneyFlowService { get; set; }

        [Inject]
        public required ISettingsService settingsService { get; set; }

        [Inject]
        public required ILoginService loginService { get; set; }



        protected override async Task OnInitializedAsync()
        {
            var user = await loginService.GetLoggedUser();
            if (user is null) return;

            priceTimeseries = await MoneyFlowService.GetAssetsTimeSeries(user.UserId, StartDateTime, DateTime.UtcNow);

            options.Tooltip = new ApexCharts.Tooltip
            {
                Y = new TooltipY
                {
                    Formatter = GetFormatter()
                }
            };
        }

        protected override async Task OnParametersSetAsync()
        {
            var user = await loginService.GetLoggedUser();
            if (user is null) return;

            priceTimeseries = await MoneyFlowService.GetAssetsTimeSeries(user.UserId, StartDateTime, DateTime.UtcNow);
            StateHasChanged();
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

        string GetFormatter()
        {
            return @"function(value, opts) {
                    if (value === undefined) {return '';}
                    return Number(value).toLocaleString() + " + $" ' {settingsService.GetCurrency()}' " + ";}";
        }
    }
}