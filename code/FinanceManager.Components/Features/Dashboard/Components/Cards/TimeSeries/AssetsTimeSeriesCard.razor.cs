using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.TimeSeries;

public partial class AssetsTimeSeriesCard
{
    private bool _isLoading;
    private bool _hasError;
    private string _currency = "PLN";
    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public string Height { get; set; } = "250px";

    [Inject] public required ILogger<AssetsTimeSeriesCard> Logger { get; set; }
    [Inject] public required AssetsPageCardsCacheService AssetsPageCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override Task OnParametersSetAsync() => Reload();

    private async Task Reload()
    {
        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            ChartData.Clear();
            return;
        }

        _isLoading = true;
        _hasError = false;
        StateHasChanged();
        try
        {
            var currency = await SettingsService.GetCurrencyAsync();
            _currency = currency.ShortName;

            ChartData.Clear();
            var context = new AssetsPageCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = currency.Id,
                StartDateTime = StartDateTime,
                EndDateTime = EndDateTime,
            };

            var snapshot = await AssetsPageCardsCacheService.GetSnapshotAsync(context);
            ChartData.AddRange(snapshot.AssetsTimeSeries.OrderBy(x => x.DateTime));
        }
        catch (Exception ex)
        {
            _hasError = true;
            Logger.LogError(ex, "Error getting assets time series data");
        }
        finally
        {
            _isLoading = false;
        }
    }
}