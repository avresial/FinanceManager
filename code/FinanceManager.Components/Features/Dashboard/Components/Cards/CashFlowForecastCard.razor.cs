using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards;

public partial class CashFlowForecastCard : ComponentBase
{
    private static readonly int[] _horizons =
    [
        CashFlowForecastHorizons.ThirtyDays,
        CashFlowForecastHorizons.SixtyDays,
        CashFlowForecastHorizons.NinetyDays
    ];

    private CashFlowForecastCardModel? _model;
    private bool _isLoading;
    private bool _hasError;
    private readonly RefreshVersionGate _gate = new();

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required CashFlowForecastHttpClient CashFlowForecastHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator SnapshotRefreshCoordinator { get; set; }
    [Inject] public required ILogger<CashFlowForecastCard> Logger { get; set; }

    protected override Task OnInitializedAsync() => LoadData();

    private async Task LoadData()
    {
        var requestVersion = _gate.Claim();
        _hasError = false;
        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                if (_gate.IsCurrent(requestVersion))
                {
                    _model = null;
                    _isLoading = false;
                }
                return;
            }

            var currency = await SettingsService.GetCurrencyAsync();
            const int horizonDays = CashFlowForecastHorizons.NinetyDays;
            var result = await SnapshotRefreshCoordinator.RunAsync(new SnapshotRefreshRequest<CashFlowForecastCardSnapshot, CashFlowForecastCardModel>
            {
                Key = $"cash-flow-forecast-card:{user.UserId}:{currency.Id}:{horizonDays}",
                Gate = _gate,
                ClaimedVersion = requestVersion,
                ToModel = snapshot => snapshot.UserId == user.UserId
                    && snapshot.CurrencyId == currency.Id
                    && snapshot.HorizonDays == horizonDays
                    ? snapshot.Model
                    : null,
                FetchAsync = async () =>
                {
                    var forecast = await CashFlowForecastHttpClient.GetAsync(user.UserId, currency.Id, horizonDays);
                    return forecast is null ? null : CashFlowForecastCardModel.FromForecast(forecast, currency.ShortName);
                },
                ToSnapshot = model => new CashFlowForecastCardSnapshot
                {
                    UserId = user.UserId,
                    CurrencyId = currency.Id,
                    HorizonDays = horizonDays,
                    Model = model
                },
                OnSnapshotPainted = ShowData,
                OnSnapshotMissing = () =>
                {
                    _isLoading = true;
                    StateHasChanged();
                    return Task.CompletedTask;
                },
                OnRefreshed = ShowData
            });

            if (!_gate.IsCurrent(requestVersion))
                return;

            if (result.IsBlockingFailure || result.Outcome == SnapshotRefreshOutcome.Empty)
            {
                _model = null;
                _hasError = true;
                _isLoading = false;
                Logger.LogError(result.Error, "Unable to load cash flow forecast.");
                StateHasChanged();
            }
        }
        catch (Exception ex)
        {
            if (!_gate.IsCurrent(requestVersion))
                return;

            _model = null;
            _isLoading = false;
            _hasError = true;
            Logger.LogError(ex, "Unable to load cash flow forecast.");
            StateHasChanged();
        }
    }

    private Task ShowData(CashFlowForecastCardModel model)
    {
        _model = model;
        _isLoading = false;
        _hasError = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    private decimal ValueAt(int horizon) => horizon switch
    {
        CashFlowForecastHorizons.ThirtyDays => _model?.ThirtyDayValue ?? 0m,
        CashFlowForecastHorizons.SixtyDays => _model?.SixtyDayValue ?? 0m,
        _ => _model?.NinetyDayValue ?? 0m
    };

    private string ExpectedSummary
    {
        get
        {
            if (_model is null) return string.Empty;

            var inflows = _model.ExpectedInflowCount;
            var outflows = _model.ExpectedOutflowCount;
            return $"{inflows} expected inflow{(inflows == 1 ? string.Empty : "s")} · "
                + $"{outflows} expected outflow{(outflows == 1 ? string.Empty : "s")}";
        }
    }

    private string FormatMoney(decimal value) => $"{value:N2} {_model?.Currency}";
}