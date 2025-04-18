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
    public partial class AssetsPerAccountOverviewCard
    {
        private string currency = "";
        private decimal TotalAssets = 0;
        private UserSession? user;
        private ApexChart<PieChartModel>? chart;

        [Parameter]
        public bool DisplayAsChart { get; set; } = true;

        [Parameter]
        public string Height { get; set; } = "300px";

        [Parameter]
        public DateTime StartDateTime { get; set; }

        [Inject]
        public required ILogger<AssetsPerAccountOverviewCard> Logger { get; set; }

        [Inject]
        public required IMoneyFlowService moneyFlowService { get; set; }

        [Inject]
        public required ISettingsService settingsService { get; set; }

        [Inject]
        public required ILoginService loginService { get; set; }

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

        public List<PieChartModel> Data { get; set; } = new List<PieChartModel>();


        protected override async Task OnInitializedAsync()
        {
            options.Tooltip = new Tooltip
            {
                Y = new TooltipY
                {
                    Formatter = ChartHelper.GetCurrencyFormatter(settingsService.GetCurrency())
                }
            };

            currency = settingsService.GetCurrency();

            await Task.CompletedTask;
        }

        protected override async Task OnParametersSetAsync()
        {
            user = await loginService.GetLoggedUser();
            if (user is null) return;

            foreach (var dataEntry in Data)
                dataEntry.Value = 0;

            if (chart is not null) await chart.UpdateSeriesAsync(true);

            await GetData();

            StateHasChanged();

            if (chart is not null) await chart.UpdateSeriesAsync(true);

        }

        async Task GetData()
        {
            Data.Clear();
            TotalAssets = 0;

            if (StartDateTime == new DateTime())
                return;

            if (user is not null)
            {
                try
                {
                    Data.AddRange(await moneyFlowService.GetEndAssetsPerAccount(user.UserId, StartDateTime, DateTime.UtcNow));
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex.ToString());
                }
            }

            if (Data.Count != 0)
                TotalAssets = Data.Sum(x => x.Value);
        }
    }
}