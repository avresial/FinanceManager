using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class AssetsPerTypeOverviewCard
    {
        private readonly AxisChartOptions _axisChartOptions = new()
        {
            MatchBoundsToSize = true,
        };
        private readonly ChartOptions _chartOptions = new()
        {
            LineStrokeWidth = 3,
            ChartPalette = ColorsProvider.GetColors().ToArray(),
            ShowLegend = false,
        };

        private bool _isLoading;
        private double[] _data = [];
        private string[] _labels = [];

        private string _currency = "";
        private decimal _totalAssets = 0;

        [Parameter] public string Height { get; set; } = "300px";
        [Parameter] public DateTime StartDateTime { get; set; }
        [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

        [Inject] public required ILogger<AssetsPerTypeOverviewCard> Logger { get; set; }
        [Inject] public required IMoneyFlowService MoneyFlowService { get; set; }
        [Inject] public required ISettingsService SettingsService { get; set; }
        [Inject] public required ILoginService LoginService { get; set; }

        protected override async Task OnInitializedAsync()
        {
            //options.Tooltip = new Tooltip
            //{
            //    Y = new TooltipY
            //    {
            //        Formatter = ChartHelper.GetCurrencyFormatter(SettingsService.GetCurrency())
            //    }
            //};

            _currency = SettingsService.GetCurrency();

            await Task.CompletedTask;
        }
        protected override async Task OnParametersSetAsync()
        {
            var data = await GetData();
            _data = data.Select(x => (double)x.Value).ToArray();
            _labels = data.Select(x => x.Name).ToArray();

            StateHasChanged();
        }

        private async Task<List<PieChartModel>> GetData()
        {
            _totalAssets = 0;
            var user = await LoginService.GetLoggedUser();

            if (StartDateTime == new DateTime()) return [];
            if (user is null) return [];

            List<PieChartModel> chartData = [];

            if (user is not null) chartData = await MoneyFlowService.GetEndAssetsPerType(user.UserId, StartDateTime, EndDateTime);
            if (chartData.Count != 0) _totalAssets = chartData.Sum(x => x.Value);

            return chartData;
        }
    }
}