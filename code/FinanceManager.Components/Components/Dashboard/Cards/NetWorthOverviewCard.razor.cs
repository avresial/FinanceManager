using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class NetWorthOverviewCard
{
    private Currency _currency = DefaultCurrency.PLN;
    private decimal? _totalNetWorth = null;
    private bool _isLoading = false;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<NetWorthOverviewCard> Logger { get; set; }
    [Inject] public required DashboardOverviewCardsCacheService DashboardOverviewCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;

        _currency = SettingsService.GetCurrency();
        _totalNetWorth = null;

        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            _isLoading = false;
            return;
        }

        try
        {
            var context = new DashboardOverviewCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = _currency.Id,
                StartDateTime = StartDateTime,
                EndDateTime = EndDateTime,
            };

            var snapshot = await DashboardOverviewCardsCacheService.GetSnapshotAsync(context);
            _totalNetWorth = snapshot.NetWorth;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while getting net worth");
        }

        _isLoading = false;
    }
}