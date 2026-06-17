using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards.Liabilities;

public partial class LiabilitiesTimeSeriesCard
{
    private bool _isLoading;
    private bool _hasError;
    private string _currency = "PLN";
    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public string Height { get; set; } = "250px";

    [Inject] public required ILogger<LiabilitiesTimeSeriesCard> Logger { get; set; }
    [Inject] public required LiabilitiesHttpClient LiabilitiesHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override Task OnParametersSetAsync() => Reload();

    private async Task Reload()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null) return;

        _isLoading = true;
        _hasError = false;
        StateHasChanged();
        try
        {
            _currency = SettingsService.GetCurrency().ShortName;

            ChartData.Clear();
            var data = await LiabilitiesHttpClient.GetLiabilitiesTimeSeries(user.UserId, StartDateTime, EndDateTime).ToListAsync();
            ChartData.AddRange(data.OrderBy(x => x.DateTime));
        }
        catch (Exception ex)
        {
            _hasError = true;
            Logger.LogError(ex, "Error getting liabilities time series data");
        }
        finally
        {
            _isLoading = false;
        }
    }
}