using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards.TimeSeries;

public partial class NetWorthTimeSeriesCard
{
    private bool _isLoading;
    public Dictionary<DateTime, decimal> ChartData { get; set; } = [];

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public string Height { get; set; } = "250px";

    [Inject] public required ILogger<NetWorthTimeSeriesCard> Logger { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            ChartData = [];
            return;
        }

        _isLoading = true;
        StateHasChanged();
        try
        {
            ChartData = await MoneyFlowHttpClient.GetNetWorth(user.UserId, SettingsService.GetCurrency(), StartDateTime, EndDateTime);
        }
        catch (Exception ex)
        {
            ChartData = [];
            Logger.LogError(ex, "Error getting net worth time series data");
        }
        finally
        {
            _isLoading = false;
        }
    }
}