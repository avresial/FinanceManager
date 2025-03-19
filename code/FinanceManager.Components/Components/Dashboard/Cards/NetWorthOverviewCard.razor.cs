using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class NetWorthOverviewCard
    {
        private string currency = string.Empty;
        private decimal TotalNetWorth = 0;

        [Parameter]
        public string Height { get; set; } = "300px";

        [Parameter]
        public DateTime StartDateTime { get; set; }

        [Inject]
        public required ILogger<NetWorthOverviewCard> Logger { get; set; }

        [Inject]
        public required IMoneyFlowService moneyFlowService { get; set; }

        [Inject]
        public required ISettingsService settingsService { get; set; }

        [Inject]
        public required ILoginService loginService { get; set; }

        protected override async Task OnParametersSetAsync()
        {
            currency = settingsService.GetCurrency();
            TotalNetWorth = 0;
            var user = await loginService.GetLoggedUser();
            if (user is null) return;
            decimal? result = null;

            try
            {
                result = await moneyFlowService.GetNetWorth(user.UserId, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while getting net worth");
            }

            if (result is not null)
                TotalNetWorth = result.Value;
        }
    }
}