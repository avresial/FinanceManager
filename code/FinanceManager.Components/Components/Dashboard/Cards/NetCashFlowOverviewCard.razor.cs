using FinanceManager.Components.Components.SharedComponents.Charts;
using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class NetCashFlowOverviewCard
{
    private Currency _currency = DefaultCurrency.PLN;
    private decimal? _totalNetCashFlow;
    private List<List<ChartJsLineDataPoint>> _series = [];
    private bool _isLoading = true;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<NetCashFlowOverviewCard> Logger { get; set; }
    [Inject] public required DashboardOverviewCardsCacheService DashboardOverviewCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        _currency = SettingsService.GetCurrency();
        _totalNetCashFlow = null;
        _series.Clear();

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
            var orderedSeries = snapshot.NetCashFlowSeries.OrderBy(x => x.DateTime).ToList();

            _totalNetCashFlow = Math.Round(orderedSeries.Sum(x => x.Value), 2);
            _series.Add(orderedSeries
                .Select(x => new ChartJsLineDataPoint(x.DateTime.ToLocalTime(), Math.Round(x.Value, 2)))
                .ToList());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while getting net cash flow");
        }

        _isLoading = false;
    }
}