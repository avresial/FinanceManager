using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Components.Features.Dashboard.Models;
using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Features.Dashboard.Cards.TimeSeries;

public partial class NetWorthTimeSeriesCard
{
    private bool _isLoading;
    private bool _hasError;
    private string _currency = "PLN";
    private List<TimeSeriesModel> _series = [];

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public string Height { get; set; } = "250px";

    // When the dashboard supplies a prepared model the card renders it directly;
    // otherwise it self-loads from the API as in standalone usage.
    [Parameter] public TimeSeriesCardModel? Model { get; set; }

    [Inject] public required ILogger<NetWorthTimeSeriesCard> Logger { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override Task OnParametersSetAsync() => Reload();

    private async Task Reload()
    {
        if (Model is not null)
        {
            _currency = (await SettingsService.GetCurrencyAsync()).ShortName;
            _series = [.. Model.Series.OrderBy(x => x.DateTime).Select(x => new TimeSeriesModel(x.DateTime, x.Value))];
            _hasError = false;
            _isLoading = false;
            return;
        }

        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            _series = [];
            return;
        }

        _isLoading = true;
        _hasError = false;
        StateHasChanged();
        try
        {
            var currency = await SettingsService.GetCurrencyAsync();
            _currency = currency.ShortName;

            var netWorth = await MoneyFlowHttpClient.GetNetWorth(user.UserId, currency, StartDateTime, EndDateTime);
            _series = netWorth.OrderBy(x => x.Key)
                              .Select(x => new TimeSeriesModel(x.Key, x.Value))
                              .ToList();
        }
        catch (Exception ex)
        {
            _series = [];
            _hasError = true;
            Logger.LogError(ex, "Error getting net worth time series data");
        }
        finally
        {
            _isLoading = false;
        }
    }
}