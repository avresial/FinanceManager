using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards;

public partial class ClosingBalanceOverviewCard
{
    private string _currency = "PLN";
    private List<TimeSeriesModel> _series = [];
    private bool _isLoading = true;
    private bool _hasError;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    [Inject] public required ILogger<ClosingBalanceOverviewCard> Logger { get; set; }
    [Inject] public required DashboardOverviewCardsCacheService DashboardOverviewCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override Task OnParametersSetAsync() => Reload();

    private async Task Reload()
    {
        var currency = SettingsService.GetCurrency();
        _currency = currency.ShortName;

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