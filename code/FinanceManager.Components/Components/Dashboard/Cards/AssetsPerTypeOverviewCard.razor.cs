using ApexCharts;
using FinanceManager.Components.Helpers;
using FinanceManager.Domain.Entities.Login;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class AssetsPerTypeOverviewCard
    {
        private string _currency = "";
        private decimal _totalAssets = 0;
        private UserSession? _user;
        private ApexChart<PieChartModel>? _chart;

        [Parameter] public string Height { get; set; } = "300px";
        [Parameter] public DateTime StartDateTime { get; set; }
        [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

        [Inject] public required ILogger<AssetsPerTypeOverviewCard> Logger { get; set; }
        [Inject] public required IMoneyFlowService MoneyFlowService { get; set; }
        [Inject] public required ISettingsService SettingsService { get; set; }
        [Inject] public required ILoginService LoginService { get; set; }


        public List<PieChartModel> ChartData { get; set; } = [];

        protected override async Task OnInitializedAsync()
        {
            options.Tooltip = new Tooltip
            {
                Y = new TooltipY
                {
                    Formatter = ChartHelper.GetCurrencyFormatter(SettingsService.GetCurrency())
                }
            };

            _currency = SettingsService.GetCurrency();

            await Task.CompletedTask;
        }
        protected override async Task OnParametersSetAsync()
        {
            _user = await LoginService.GetLoggedUser();
            if (_user is null) return;

            foreach (var dataEntry in ChartData)
                dataEntry.Value = 0;

            await GetData();
            StateHasChanged();

            if (_chart is not null) await _chart.UpdateSeriesAsync(true);
        }

        private async Task GetData()
        {
            if (StartDateTime == new DateTime())
            {
                ChartData.Clear();
                _totalAssets = 0;
                return;
            }

            if (_user is not null)
            {
                try
                {
                    ChartData = await MoneyFlowService.GetEndAssetsPerType(_user.UserId, StartDateTime, EndDateTime);
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }
            }

            _totalAssets = ChartData.Sum(x => x.Value);
        }

        private ApexChartOptions<PieChartModel> options { get; set; } = new()
        {
            Chart = new Chart
            {
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