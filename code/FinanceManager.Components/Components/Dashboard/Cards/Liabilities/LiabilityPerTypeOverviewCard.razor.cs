using ApexCharts;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards.Liabilities
{
    public partial class LiabilityPerTypeOverviewCard
    {
        private string _currency = "";
        private decimal _totalLiabilities = 0;
        private ApexChart<PieChartModel>? _chart;

        [Parameter] public string Height { get; set; } = "300px";
        [Parameter] public DateTime StartDateTime { get; set; }

        [Inject] public required ILogger<LiabilityPerTypeOverviewCard> Logger { get; set; }
        [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
        [Inject] public required ILiabilitiesService LiabilitiesService { get; set; }
        [Inject] public required ISettingsService SettingsService { get; set; }
        [Inject] public required ILoginService LoginService { get; set; }

        public List<PieChartModel> ChartData { get; set; } = [];

        protected override void OnInitialized()
        {
            _currency = SettingsService.GetCurrency();
        }

        protected override async Task OnParametersSetAsync()
        {
            ChartData.Clear();
            ChartData.AddRange(await GetData());

            StateHasChanged();
            if (_chart is not null) await _chart.UpdateSeriesAsync(true);
        }

        private async Task<List<PieChartModel>> GetData()
        {
            if (StartDateTime == new DateTime())
                return [];

            var user = await LoginService.GetLoggedUser();
            if (user is null) return [];
            List<PieChartModel> result = [];
            try
            {
                result = await LiabilitiesService.GetEndLiabilitiesPerType(user.UserId, StartDateTime, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error getting assets time series data");
            }

            if (result.Count != 0)
                _totalLiabilities = result.Sum(x => x.Value);

            foreach (var data in result)
                data.Value *= -1;

            return result;
        }


        private ApexChartOptions<PieChartModel> _options { get; set; } = new()
        {
            Chart = new Chart
            {
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
                Position = XAxisPosition.Bottom,
                Type = XAxisType.Category

            },
            Yaxis = new List<YAxis>()
            {

                new YAxis
                {
                    AxisTicks = new AxisTicks()
                    {
                        Show = false
                    },
                    Show = false,
                    SeriesName = "NetValue",
                    DecimalsInFloat = 0,
                }
            },
            Legend = new Legend()
            {
                Position = LegendPosition.Bottom,
            },
            Colors = ColorsProvider.GetColors()
        };
    }
}