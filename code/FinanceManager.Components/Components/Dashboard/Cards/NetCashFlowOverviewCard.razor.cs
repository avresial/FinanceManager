using FinanceManager.Components.Components.SharedComponents.Charts;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
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
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
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
            var netCashFlow = await MoneyFlowHttpClient.GetNetCashFlow(user.UserId, DefaultCurrency.PLN, StartDateTime.Date, EndDateTime);
            var orderedSeries = netCashFlow.OrderBy(x => x.DateTime).ToList();

            _totalNetCashFlow = orderedSeries.Sum(x => x.Value);
            _series.Add(orderedSeries
                .Select(x => new ChartJsLineDataPoint(x.DateTime.ToLocalTime(), x.Value))
                .ToList());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while getting net cash flow");
        }

        _isLoading = false;
    }
}