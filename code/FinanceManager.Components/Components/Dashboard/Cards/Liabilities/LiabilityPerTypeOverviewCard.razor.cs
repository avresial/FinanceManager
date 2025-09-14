using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Providers;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards.Liabilities
{
    public partial class LiabilityPerTypeOverviewCard
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
        private decimal _totalLiabilities = 0;

        [Parameter] public string Height { get; set; } = "300px";
        [Parameter] public DateTime StartDateTime { get; set; }
        [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

        [Inject] public required ILogger<LiabilityPerTypeOverviewCard> Logger { get; set; }
        [Inject] public required IFinancialAccountService FinancialAccountService { get; set; }
        [Inject] public required ILiabilitiesService LiabilitiesService { get; set; }
        [Inject] public required ISettingsService SettingsService { get; set; }
        [Inject] public required ILoginService LoginService { get; set; }


        protected override void OnInitialized()
        {
            _currency = SettingsService.GetCurrency();
        }

        protected override async Task OnParametersSetAsync()
        {
            List<NameValueResult> data = await GetData();

            _data = data.Select(x => (double)x.Value).ToArray();
            _labels = data.Select(x => x.Name).ToArray();

            StateHasChanged();
        }

        private async Task<List<NameValueResult>> GetData()
        {
            if (StartDateTime == new DateTime())
                return [];

            var user = await LoginService.GetLoggedUser();
            if (user is null) return [];
            List<NameValueResult> result = [];
            try
            {
                result = await LiabilitiesService.GetEndLiabilitiesPerType(user.UserId, StartDateTime, EndDateTime).ToListAsync();
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
    }
}