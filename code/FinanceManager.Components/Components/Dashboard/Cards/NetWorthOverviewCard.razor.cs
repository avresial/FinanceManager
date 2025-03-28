using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class NetWorthOverviewCard
    {
        private string _currency = string.Empty;
        private decimal _totalNetWorth = 0;

        [Parameter]
        public string Height { get; set; } = "300px";

        [Parameter]
        public DateTime StartDateTime { get; set; }

        [Inject]
        public required ILogger<NetWorthOverviewCard> Logger { get; set; }

        [Inject]
        public required IMoneyFlowService MoneyFlowService { get; set; }

        [Inject]
        public required ISettingsService SettingsService { get; set; }

        [Inject]
        public required ILoginService LoginService { get; set; }

        protected override async Task OnParametersSetAsync()
        {
            _currency = SettingsService.GetCurrency();
            _totalNetWorth = 0;
            var user = await LoginService.GetLoggedUser();
            if (user is null) return;
            decimal? result = null;

            try
            {
                result = await MoneyFlowService.GetNetWorth(user.UserId, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while getting net worth");
            }

            if (result is not null)
                _totalNetWorth = result.Value;
        }
    }
}