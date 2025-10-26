using FinanceManager.Components.HttpContexts;
using FinanceManager.Domain.Entities;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards
{
    public partial class NetWorthOverviewCard
    {
        private Currency _currency = DefaultCurrency.Currency;
        private decimal? _totalNetWorth = null;
        private bool _isLoading = false;

        [Parameter] public string Height { get; set; } = "300px";
        [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

        [Inject] public required ILogger<NetWorthOverviewCard> Logger { get; set; }
        [Inject] public required MoneyFlowHttpContext MoneyFlowHttpContext { get; set; }
        [Inject] public required ISettingsService SettingsService { get; set; }
        [Inject] public required ILoginService LoginService { get; set; }

        protected override async Task OnParametersSetAsync()
        {
            _isLoading = true;

            _currency = SettingsService.GetCurrency();
            _totalNetWorth = null;

            var user = await LoginService.GetLoggedUser();
            if (user is null) return;

            decimal? result = null;

            try
            {
                result = await MoneyFlowHttpContext.GetNetWorth(user.UserId, DefaultCurrency.Currency, EndDateTime);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while getting net worth");
            }

            if (result is not null)
                _totalNetWorth = result.Value;

            _isLoading = false;
        }
    }
}