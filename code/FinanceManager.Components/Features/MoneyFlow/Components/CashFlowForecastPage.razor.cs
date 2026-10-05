using ApexCharts;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Features.MoneyFlow.Models;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Features.MoneyFlow.Components;

public partial class CashFlowForecastPage : ComponentBase, IDisposable
{
    private readonly ApexChartOptions<TimeSeriesModel> _chartOptions = BuildChartOptions();
    private CancellationTokenSource? _loadCancellationTokenSource;
    private CashFlowForecast? _forecast;
    private readonly RefreshVersionGate _gate = new();
    private string? _paintedKey;
    private int _horizonDays = CashFlowForecastHorizons.NinetyDays;
    private string _currency = "PLN";
    private bool _isLoading = true;
    private bool _hasError;

    [Inject] public required CashFlowForecastHttpClient CashFlowForecastHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator SnapshotRefreshCoordinator { get; set; }
    [Inject] public required ILogger<CashFlowForecastPage> Logger { get; set; }

    public IReadOnlyList<int> Horizons => CashFlowForecastHorizons.All;

    protected override Task OnInitializedAsync() => LoadData();

    private async Task SelectHorizon(int horizonDays)
    {
        if (_horizonDays == horizonDays) return;
        _horizonDays = horizonDays;
        await LoadData();
    }

    private async Task LoadData()
    {
        var requestVersion = _gate.Claim();
        var horizonDays = _horizonDays;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var previousRequest = Interlocked.Exchange(ref _loadCancellationTokenSource, cancellationTokenSource);
        previousRequest?.Cancel();
        previousRequest?.Dispose();

        if (_forecast?.HorizonDays != horizonDays)
        {
            _forecast = null;
            _paintedKey = null;
        }
        _isLoading = _forecast is null;
        _hasError = false;

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (!_gate.IsCurrent(requestVersion)) return;

            if (user is null)
            {
                _forecast = null;
                _paintedKey = null;
                return;
            }

            var currency = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(requestVersion)) return;

            var key = $"cash-flow-forecast-page:{user.UserId}:{currency.Id}:{horizonDays}";
            if (_paintedKey != key)
                _forecast = null;
            _isLoading = _forecast is null;

            var result = await SnapshotRefreshCoordinator.RunAsync(new SnapshotRefreshRequest<CashFlowForecastPageSnapshot, CashFlowForecastPageModel>
            {
                Key = key,
                Gate = _gate,
                ClaimedVersion = requestVersion,
                ToModel = snapshot => snapshot.UserId == user.UserId
                    && snapshot.CurrencyId == currency.Id
                    && snapshot.HorizonDays == horizonDays
                    && snapshot.Model?.Forecast is { HistoricalSeries: not null, ForecastSeries: not null, ExpectedTransactions: not null } forecast
                    && forecast.UserId == user.UserId
                    && forecast.CurrencyId == currency.Id
                    && forecast.HorizonDays == horizonDays
                    && !string.IsNullOrWhiteSpace(snapshot.Model.Currency)
                    ? snapshot.Model
                    : null,
                FetchAsync = async () =>
                {
                    var forecast = await CashFlowForecastHttpClient.GetAsync(user.UserId, currency.Id, horizonDays, cancellationToken)
                        ?? throw new HttpRequestException("Cash-flow forecast response was empty.");
                    return new CashFlowForecastPageModel(forecast, currency.ShortName);
                },
                ToSnapshot = model => new CashFlowForecastPageSnapshot
                {
                    UserId = user.UserId,
                    CurrencyId = currency.Id,
                    HorizonDays = horizonDays,
                    Model = model
                },
                OnSnapshotPainted = model => ShowData(model, key),
                OnSnapshotMissing = () =>
                {
                    _isLoading = _forecast is null;
                    StateHasChanged();
                    return Task.CompletedTask;
                },
                OnRefreshed = model => ShowData(model, key)
            });
            if (!_gate.IsCurrent(requestVersion)) return;

            if (result.Outcome == SnapshotRefreshOutcome.Failed)
            {
                _hasError = true;
                Logger.LogError(result.Error, "Unable to refresh cash flow forecast page.");
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!_gate.IsCurrent(requestVersion)) return;

            _hasError = true;
            Logger.LogError(ex, "Unable to load cash flow forecast page.");
        }
        finally
        {
            if (_gate.IsCurrent(requestVersion))
                _isLoading = false;

            if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _loadCancellationTokenSource, null, cancellationTokenSource),
                    cancellationTokenSource))
            {
                cancellationTokenSource.Dispose();
            }
        }
    }

    private Task ShowData(CashFlowForecastPageModel model, string key)
    {
        _forecast = model.Forecast;
        _currency = model.Currency;
        _paintedKey = key;
        _isLoading = false;
        _hasError = false;
        StateHasChanged();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _gate.Claim();
        var request = Interlocked.Exchange(ref _loadCancellationTokenSource, null);
        request?.Cancel();
        request?.Dispose();
    }

    private string FormatAmount(decimal amount) => MoneyFormatter.FormatSigned(amount, _currency);

    private static ApexChartOptions<TimeSeriesModel> BuildChartOptions() => new()
    {
        Chart = new Chart
        {
            Background = "transparent",
            Toolbar = new Toolbar { Show = false },
            Zoom = new Zoom { Enabled = false },
            FontFamily = "Roboto, sans-serif"
        },
        Colors = ["#ffab00", "#42a5f5"],
        Stroke = new Stroke { Curve = Curve.Stepline, Width = 2, LineCap = LineCap.Round },
        DataLabels = new DataLabels { Enabled = false },
        Legend = new Legend
        {
            Show = true,
            Position = LegendPosition.Top,
            HorizontalAlign = ApexCharts.Align.Right
        },
        Fill = new Fill
        {
            Type = [FillType.Gradient, FillType.Solid],
            Gradient = new FillGradient { OpacityFrom = 0.45, OpacityTo = 0d, Stops = [0, 100] },
            Opacity = [1d, 1d]
        },
        Grid = new Grid
        {
            BorderColor = "rgba(128,128,128,0.18)",
            StrokeDashArray = 0,
            Xaxis = new GridXAxis { Lines = new Lines { Show = false } },
            Yaxis = new GridYAxis { Lines = new Lines { Show = true } }
        },
        Xaxis = new XAxis
        {
            Type = XAxisType.Datetime,
            AxisBorder = new AxisBorder { Show = false },
            AxisTicks = new AxisTicks { Show = false },
            Labels = new XAxisLabels
            {
                Style = new AxisLabelStyle { Colors = "rgba(130,130,130,0.95)", FontSize = "11px" },
                DatetimeFormatter = new DatetimeFormatter { Year = "yyyy", Month = "MMM 'yy", Day = "dd MMM" }
            }
        },
        Yaxis =
        [
            new YAxis
            {
                Floating = true,
                AxisBorder = new AxisBorder { Show = false },
                AxisTicks = new AxisTicks { Show = false },
                Labels = new YAxisLabels
                {
                    OffsetX = 38,
                    Style = new AxisLabelStyle { Colors = "rgba(130,130,130,0.95)", FontSize = "11px" },
                    Formatter = ChartHelper.CompactCurrencyTickFormatter
                }
            }
        ],
        Tooltip = new Tooltip
        {
            Enabled = true,
            Shared = true,
            Intersect = false,
            X = new TooltipX { Format = "dd MMM yyyy" }
        }
    };
}