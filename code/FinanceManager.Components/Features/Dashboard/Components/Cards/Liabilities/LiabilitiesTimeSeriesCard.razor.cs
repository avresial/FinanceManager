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
    private (int UserId, int CurrencyId)? _owner;
    private bool _hasDisplayedModel;

    // Guards against a slower, earlier reload overwriting a newer date/currency selection: every
    // Reload claims an incrementing version and only commits its result if it is still the latest.
    private readonly RefreshVersionGate _gate = new();

    public List<TimeSeriesModel> ChartData { get; set; } = [];

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public string Height { get; set; } = "250px";

    // Reports whether a reload for the selected range is in flight, so the page can show the refresh indicator.
    [Parameter] public EventCallback<bool> RefreshingChanged { get; set; }

    [Inject] public required ILogger<LiabilitiesTimeSeriesCard> Logger { get; set; }
    [Inject] public required LiabilitiesHttpClient LiabilitiesHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required LiabilitiesSnapshotStore SnapshotStore { get; set; }

    protected override Task OnParametersSetAsync() => Reload();

    // Stale-while-revalidate: paint the stored chart, always re-fetch, and only repaint and
    // re-persist when the fresh series differs. LiabilitiesSnapshotStore / SnapshotRefreshCoordinator
    // own that workflow — see docs/architecture/concepts/ui-snapshots.md.
    private async Task Reload()
    {
        // Claimed here rather than inside the coordinator so the same version also guards the
        // logged-out branch below, which commits state before the run starts.
        var requestVersion = _gate.Claim();
        try
        {
            await RefreshingChanged.InvokeAsync(true);
            await LoadAsync(requestVersion);
        }
        finally
        {
            // A superseded reload leaves the flag alone: the newer reload owns it now.
            if (_gate.IsCurrent(requestVersion)) await RefreshingChanged.InvokeAsync(false);
        }
    }

    private async Task LoadAsync(int requestVersion)
    {
        var startDate = StartDateTime;
        var endDate = EndDateTime;

        _hasError = false;

        var user = await LoginService.GetLoggedUser();
        if (user is null)
        {
            if (_gate.IsCurrent(requestVersion))
            {
                ChartData = [];
                _owner = null;
                _hasDisplayedModel = false;
                _isLoading = false;
                StateHasChanged();
            }
            return;
        }

        var currency = await SettingsService.GetCurrencyAsync();
        if (!_gate.IsCurrent(requestVersion)) return;
        if (_owner != (user.UserId, currency.Id))
        {
            ChartData = [];
            _hasDisplayedModel = false;
        }
        _owner = (user.UserId, currency.Id);
        _currency = currency.ShortName;

        var result = await SnapshotStore.RefreshTimeSeriesAsync(
            user.UserId,
            _currency,
            startDate,
            endDate,
            _gate,
            requestVersion,
            fetchAsync: async () =>
            {
                var data = await LiabilitiesHttpClient.GetLiabilitiesTimeSeries(user.UserId, startDate, endDate).ToListAsync();
                return new TimeSeriesCardModel([.. data.OrderBy(x => x.DateTime)]);
            },
            onSnapshotPainted: ShowData,
            onSnapshotMissing: () =>
            {
                _isLoading = !_hasDisplayedModel;
                StateHasChanged();
                return Task.CompletedTask;
            },
            onRefreshed: ShowData);

        if (!_gate.IsCurrent(requestVersion))
            return;

        _hasError = result.Error is not null;
        if (_hasError) Logger.LogError(result.Error, "Error getting liabilities time series data");

        _isLoading = false;
        StateHasChanged();
    }

    private Task ShowData(TimeSeriesCardModel model)
    {
        ChartData = model.Series;
        _hasDisplayedModel = true;
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }
}