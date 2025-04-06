using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class SpendingOverviewCard
    {
        private string _currency = "";
        private ApexChart<TimeSeriesModel> _chart = new();
        private ApexChartOptions<TimeSeriesModel> _options = new();

        [Parameter] public List<TimeSeriesModel> Data { get; set; } = [];
        [Parameter] public DateTime StartDateTime { get; set; }
        [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
        [Parameter] public string Height { get; set; } = "300px";

        [Inject] public required ILogger<SpendingCathegoryOverviewCard> Logger { get; set; }
        [Inject] public required IMoneyFlowService MoneyFlowService { get; set; }
        [Inject] public required IFinancialAccountService FinancalAccountService { get; set; }
        [Inject] public required ISettingsService settingsService { get; set; }
        [Inject] public required ILoginService loginService { get; set; }

        public decimal TotalSpending = 0;

        protected override async Task OnParametersSetAsync()
        {
            Data.Clear();

            var user = await loginService.GetLoggedUser();
            if (user is null) return;

            List<TimeSeriesModel> spending = [];

            try
            {
                spending = await MoneyFlowService.GetSpending(user.UserId, StartDateTime, EndDateTime);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting spending time series data");
            }

            Data.AddRange(spending);
            TotalSpending = Data.Sum(x => x.Value);

            if (_chart is not null) await _chart.UpdateSeriesAsync(true);
        }
        protected override void OnInitialized()
        {
            _currency = settingsService.GetCurrency();
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

            _options.Tooltip = new ApexCharts.Tooltip
            {
                Y = new TooltipY
                {
                    Formatter = ChartHelper.GetCurrencyFormatter(settingsService.GetCurrency())
                }
            };
            _options.Colors = new List<string>
            {
               ColorsProvider.GetColors().First()
            };

        }


    }
}