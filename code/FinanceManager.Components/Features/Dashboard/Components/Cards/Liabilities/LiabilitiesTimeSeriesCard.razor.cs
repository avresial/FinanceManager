using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Liabilities;

public partial class LiabilitiesTimeSeriesCard
{
    private bool _isLoading;
    private bool _hasError;
    private string _currency = "PLN";

    // Guards against a slower, earlier reload overwriting a newer date/currency selection: every
    // Reload claims an incrementing version and only commits its result if it is still the latest.
    private readonly RefreshVersionGate _gate = new();

    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public string Height { get; set; } = "250px";

    [Inject] public required ILogger<LiabilitiesTimeSeriesCard> Logger { get; set; }
    [Inject] public required LiabilitiesHttpClient LiabilitiesHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required LiabilitiesSnapshotStore SnapshotStore { get; set; }

    protected override Task OnParametersSetAsync() => Reload();

    // Stale-while-revalidate: paint the stored chart, always re-fetch, and only repaint and
    // re-persist when the fresh series differs. LiabilitiesSnapshotStore / SnapshotRefreshCoordinator
    // own that workflow — see docs/codebase/UI-SNAPSHOTS.md.
    private async Task Reload()
    {
        // Claimed here rather than inside the coordinator so the same version also guards the
        // logged-out branch below, which commits state before the run starts.
        var requestVersion = _gate.Claim();
        var startDate = StartDateTime;
        var endDate = EndDateTime;

        _hasError = false;

        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            if (_gate.IsCurrent(requestVersion))
            {
                ChartData = [];
                _isLoading = false;
                StateHasChanged();
            }
            return;
        }

        _currency = (await SettingsService.GetCurrencyAsync()).ShortName;

        var result = await SnapshotStore.RefreshTimeSeriesAsync(
            user.UserId,
            _currency,
            startDate,
            endDate,
            _gate,
            fetchAsync: async () =>
            {
                var data = await LiabilitiesHttpClient.GetLiabilitiesTimeSeries(user.UserId, startDate, endDate).ToListAsync();
                return new TimeSeriesCardModel([.. data.OrderBy(x => x.DateTime)]);
            },
            onSnapshotPainted: ShowData,
            onSnapshotMissing: () =>
            {
                _isLoading = true;
                StateHasChanged();
                return Task.CompletedTask;
            },
            onRefreshed: ShowData);

        if (!_gate.IsCurrent(requestVersion))
            return;

        // A failed fetch behind a painted snapshot keeps the chart on screen; only a surface with
        // nothing to show falls back to the error state.
        if (result.IsBlockingFailure)
        {
            Logger.LogError(result.Error, "Error getting liabilities time series data");
            ChartData = [];
            _hasError = true;
        }

        _isLoading = false;
        StateHasChanged();
    }

    private Task ShowData(TimeSeriesCardModel model)
    {
        ChartData = model.Series;
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }
}