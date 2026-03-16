using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards.TimeSeries;

public partial class AssetsTimeSeriesCard
{
    private bool _isLoading;
    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public string Height { get; set; } = "250px";

    [Inject] public required ILogger<AssetsTimeSeriesCard> Logger { get; set; }
    [Inject] public required AssetsPageCardsCacheService AssetsPageCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null) return;

        _isLoading = true;
        StateHasChanged();
        try
        {
            ChartData.Clear();
            var context = new AssetsPageCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = SettingsService.GetCurrency().Id,
                StartDateTime = StartDateTime,
                EndDateTime = EndDateTime,
            };

            var snapshot = await AssetsPageCardsCacheService.GetSnapshotAsync(context);
            ChartData.AddRange(snapshot.AssetsTimeSeries.OrderBy(x => x.DateTime));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex.Message, ex);
        }

        _isLoading = false;
    }
}