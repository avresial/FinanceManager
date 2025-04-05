using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class IncomeOverviewCard
    {
        private string _currency = string.Empty;
        private ApexChart<TimeSeriesModel>? _chart;
        private ApexChartOptions<TimeSeriesModel> _options = new();

        public List<TimeSeriesModel> ChartData { get; set; } = [];

        [Inject] public required ILogger<IncomeOverviewCard> Logger { get; set; }
        [Inject] public required IMoneyFlowService MoneyFlowService { get; set; }
        [Inject] public required ISettingsService settingsService { get; set; }
        [Inject] public required ILoginService loginService { get; set; }

        [Parameter] public string Height { get; set; } = "300px";
        [Parameter] public DateTime StartDateTime { get; set; }

        public decimal TotalIncome = 0;

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
                }
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
                TickAmount = 2,
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
        private async Task<List<TimeSeriesModel>> GetData()
        {
            var user = await loginService.GetLoggedUser();
            if (user is null) return [];

            var result = await MoneyFlowService.GetIncome(user.UserId, StartDateTime, DateTime.UtcNow);

            TotalIncome = result.Sum(x => x.Value);

            return result.OrderBy(x => x.DateTime).ToList();
        }

        protected override async Task OnParametersSetAsync()
        {
            ChartData.Clear();
            ChartData.AddRange(await GetData());

            if (_chart is not null) await _chart.UpdateSeriesAsync(true);
        }
    }
}