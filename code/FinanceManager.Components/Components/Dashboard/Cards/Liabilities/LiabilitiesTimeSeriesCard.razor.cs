using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards.Liabilities;
public partial class LiabilitiesTimeSeriesCard
{

    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<LiabilitiesTimeSeriesCard> Logger { get; set; }
    [Inject] public required ILiabilitiesService LiabilitiesService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null) return;
        ChartData.Clear();
        ChartData.AddRange((await GetData()).OrderBy(x => x.DateTime));
    }

    private async Task<List<TimeSeriesModel>> GetData()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null) return [];

        try
        {
            return await LiabilitiesService.GetLiabilitiesTimeSeries(user.UserId, StartDateTime, EndDateTime);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error getting assets time series data");
        }

        return [];
    }
}