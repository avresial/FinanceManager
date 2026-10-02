using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.TimeSeries;

public partial class NetWorthTimeSeriesCard
{
    private readonly RefreshVersionGate _gate = new();
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
    [Inject] public required ISnapshotRefreshCoordinator Coordinator { get; set; }

    protected override Task OnParametersSetAsync() => Reload();

    private async Task Reload()
    {
        // Claimed first on both paths so a parent-supplied model supersedes any in-flight self-load.
        var version = _gate.Claim();
        if (Model is not null)
        {
            var model = Model;
            var supplied = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(version)) return;
            _currency = supplied.ShortName;
            _series = [.. model.Series.OrderBy(x => x.DateTime).Select(x => new TimeSeriesModel(x.DateTime, x.Value))];
            _hasError = false;
            _isLoading = false;
            return;
        }

        var start = StartDateTime.Date;
        var end = EndDateTime;
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (!_gate.IsCurrent(version)) return;
            if (user is null)
            {
                _series = [];
                _isLoading = false;
                _hasError = false;
                return;
            }

            var currency = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(version)) return;
            _currency = currency.ShortName;
            _hasError = false;
            var result = await Coordinator.RunAsync(new SnapshotRefreshRequest<NetWorthTimeSeriesSnapshot, TimeSeriesCardModel>
            {
                Key = $"networth-timeseries:{user.UserId}:{currency.Id}",
                Gate = _gate,
                ClaimedVersion = version,
                // The range is metadata: never show old amounts under a different range.
                ToModel = snapshot => snapshot.UserId == user.UserId && snapshot.CurrencyId == currency.Id
                    && snapshot.StartDate == start && snapshot.EndDate.Date == end.Date
                    ? new TimeSeriesCardModel(snapshot.Series) : null,
                FetchAsync = async () =>
                {
                    var netWorth = await MoneyFlowHttpClient.GetNetWorth(user.UserId, currency, start, end);
                    return new TimeSeriesCardModel([.. netWorth.OrderBy(x => x.Key).Select(x => new TimeSeriesModel(x.Key, x.Value))]);
                },
                ToSnapshot = model => new NetWorthTimeSeriesSnapshot
                {
                    UserId = user.UserId,
                    CurrencyId = currency.Id,
                    StartDate = start,
                    EndDate = end,
                    Series = [.. model.Series],
                },
                OnSnapshotPainted = ShowData,
                OnSnapshotMissing = () =>
                {
                    _series = [];
                    _isLoading = true;
                    StateHasChanged();
                    return Task.CompletedTask;
                },
                OnRefreshed = ShowData,
            });
            if (!_gate.IsCurrent(version)) return;
            _hasError = result.IsBlockingFailure;
            _isLoading = false;
        }
        catch (Exception ex)
        {
            if (!_gate.IsCurrent(version)) return;
            _series = [];
            _hasError = true;
            _isLoading = false;
            Logger.LogError(ex, "Error getting net worth time series data");
        }
    }

    private Task ShowData(TimeSeriesCardModel model)
    {
        _series = [.. model.Series];
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }
}