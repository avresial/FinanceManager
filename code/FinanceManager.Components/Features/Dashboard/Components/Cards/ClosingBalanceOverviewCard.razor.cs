using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class ClosingBalanceOverviewCard
{
    private readonly RefreshVersionGate _gate = new();
    private string _currency = "PLN";
    private List<TimeSeriesModel> _series = [];
    private bool _isLoading = true;
    private bool _hasError;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    // When the dashboard supplies a prepared model the card renders it directly;
    // otherwise it self-loads, painting the last snapshot while it always refetches.
    [Parameter] public TimeSeriesCardModel? Model { get; set; }

    [Inject] public required ILogger<ClosingBalanceOverviewCard> Logger { get; set; }
    [Inject] public required DashboardOverviewCardsCacheService DashboardOverviewCardsCacheService { get; set; }
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
            _series = ToRenderedSeries(model.Series);
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
            var currency = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(version)) return;
            _currency = currency.ShortName;
            if (user is null)
            {
                _series = [];
                _hasError = false;
                _isLoading = false;
                return;
            }

            _hasError = false;
            var context = new DashboardOverviewCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = currency.Id,
                StartDateTime = start,
                EndDateTime = end,
            };
            var result = await Coordinator.RunAsync(new SnapshotRefreshRequest<ClosingBalanceSnapshot, TimeSeriesCardModel>
            {
                Key = $"closing-balance:{user.UserId}:{currency.Id}",
                Gate = _gate,
                ClaimedVersion = version,
                // The range is metadata: never show old amounts under a different range.
                ToModel = snapshot => snapshot.UserId == user.UserId && snapshot.CurrencyId == currency.Id
                    && snapshot.StartDate == start && snapshot.EndDate.Date == end.Date
                    ? new TimeSeriesCardModel(snapshot.Series) : null,
                FetchAsync = async () =>
                {
                    var fresh = await DashboardOverviewCardsCacheService.GetFreshAsync(context);
                    return new TimeSeriesCardModel(ToRenderedSeries(fresh.ClosingBalanceSeries));
                },
                ToSnapshot = model => new ClosingBalanceSnapshot
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
            Logger.LogError(ex, "Error while getting closing balance");
        }
    }

    private Task ShowData(TimeSeriesCardModel model)
    {
        _series = ToRenderedSeries(model.Series);
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private static List<TimeSeriesModel> ToRenderedSeries(IEnumerable<TimeSeriesModel> series) =>
        [.. series.OrderBy(x => x.DateTime).Select(x => new TimeSeriesModel(x.DateTime, Math.Round(x.Value, 2)))];
}