using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.TimeSeries;

public partial class AssetsTimeSeriesCardContainer
{
    private readonly RefreshVersionGate _gate = new();
    private TimeSeriesCardModel? _model;
    private bool _isLoading;
    private bool _hasError;
    private string _currency = "PLN";

    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; }
    [Parameter] public string Height { get; set; } = "250px";

    // Reports whether a reload for the selected range is in flight, so the page can show the refresh indicator.
    [Parameter] public EventCallback<bool> RefreshingChanged { get; set; }

    [Inject] public required ISnapshotRefreshCoordinator Coordinator { get; set; }
    [Inject] public required AssetsPageCardsCacheService AssetsCache { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ILogger<AssetsTimeSeriesCardContainer> Logger { get; set; }

    protected override Task OnParametersSetAsync() => Reload();

    private async Task Reload()
    {
        var version = _gate.Claim();
        var start = StartDateTime.Date;
        var end = EndDateTime;
        await RefreshingChanged.InvokeAsync(true);
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (!_gate.IsCurrent(version)) return;
            if (user is null)
            {
                _model = null;
                _isLoading = false;
                _hasError = false;
                return;
            }

            var currency = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(version)) return;
            _currency = currency.ShortName;
            _hasError = false;
            var context = new AssetsPageCardsRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = currency.Id,
                StartDateTime = start,
                EndDateTime = end,
            };
            var result = await Coordinator.RunAsync(new SnapshotRefreshRequest<AssetsTimeSeriesSnapshot, TimeSeriesCardModel>
            {
                Key = $"assets-timeseries:{user.UserId}:{currency.Id}",
                Gate = _gate,
                ClaimedVersion = version,
                // Point dates are authoritative; never relabel a previously rendered range as today's range.
                ToModel = snapshot => snapshot.UserId == user.UserId && snapshot.CurrencyId == currency.Id
                    && snapshot.StartDate == start && snapshot.EndDate.Date == end.Date
                    ? new TimeSeriesCardModel(snapshot.Series) : null,
                FetchAsync = async () => new TimeSeriesCardModel([.. (await AssetsCache.GetFreshTimeSeriesAsync(context)).OrderBy(x => x.DateTime)]),
                ToSnapshot = model => new AssetsTimeSeriesSnapshot
                {
                    UserId = user.UserId,
                    CurrencyId = currency.Id,
                    StartDate = start,
                    EndDate = end,
                    Series = model.Series,
                },
                OnSnapshotPainted = ShowData,
                OnSnapshotMissing = () =>
                {
                    _model = null;
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
        catch (Exception exception)
        {
            if (!_gate.IsCurrent(version)) return;
            _model = null;
            _hasError = true;
            _isLoading = false;
            Logger.LogError(exception, "Error resolving assets time series context");
        }
        finally
        {
            // A superseded reload leaves the flag alone: the newer reload owns it now.
            if (_gate.IsCurrent(version)) await RefreshingChanged.InvokeAsync(false);
        }
    }

    private Task ShowData(TimeSeriesCardModel model)
    {
        _model = model;
        _isLoading = false;
        StateHasChanged();
        return Task.CompletedTask;
    }
}