using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Components.Features.Dashboard.Models;
using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards;

public partial class ClosingBalanceOverviewCard
{
    private string _currency = "PLN";
    private List<TimeSeriesModel> _series = [];
    private bool _isLoading = true;
    private bool _hasError;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    // When the dashboard supplies a prepared model the card renders it directly;
    // otherwise it self-loads from the cache service as in standalone usage.
    [Parameter] public TimeSeriesCardModel? Model { get; set; }

    [Inject] public required ILogger<ClosingBalanceOverviewCard> Logger { get; set; }
    [Inject] public required DashboardOverviewCardsCacheService DashboardOverviewCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override Task OnParametersSetAsync() => Reload();

    private async Task Reload()
    {
        var currency = SettingsService.GetCurrency();
        _currency = currency.ShortName;

        if (Model is not null)
        {
            _series = [.. Model.Series.OrderBy(x => x.DateTime).Select(x => new TimeSeriesModel(x.DateTime, Math.Round(x.Value, 2)))];
            _hasError = false;
            _isLoading = false;
            return;
        }

        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            _series = [];
            _hasError = false;
            _isLoading = false;
            return;
        }

        _isLoading = true;
        _hasError = false;
        StateHasChanged();

        try
        {
            var context = new DashboardOverviewCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = currency.Id,
                StartDateTime = StartDateTime,
                EndDateTime = EndDateTime,
            };

            var snapshot = await DashboardOverviewCardsCacheService.GetSnapshotAsync(context);
            _series = snapshot.ClosingBalanceSeries
                .OrderBy(x => x.DateTime)
                .Select(x => new TimeSeriesModel(x.DateTime, Math.Round(x.Value, 2)))
                .ToList();
        }
        catch (Exception ex)
        {
            _series = [];
            _hasError = true;
            Logger.LogError(ex, "Error while getting closing balance");
        }
        finally
        {
            _isLoading = false;
        }
    }
}