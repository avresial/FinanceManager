using ApexCharts;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Helpers;
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
    private ApexChartOptions<TimeSeriesModel>? _chartOptions;
    private int _chartVersion;
    private bool HasChartSeries => _model?.HasForecastableActivity == true && _model.ForecastPoints is { Count: > 0 };
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
        BuildChart(model);
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

    private decimal DeltaAt(int horizon) => ValueAt(horizon) - (_model?.CurrentBalance ?? 0m);

    private void BuildChart(CashFlowForecastCardModel model)
    {
        _chartVersion++;
        _chartOptions = null;
        if (!HasChartSeries) return;

        // Use the projection's date, including when a previous day's snapshot paints first.
        var today = model.ForecastPoints![0].DateTime.Date;
        var muted = "var(--mud-palette-text-secondary)";
        _chartOptions = new ApexChartOptions<TimeSeriesModel>
        {
            Chart = new Chart
            {
                Toolbar = new Toolbar { Show = false },
                Zoom = new Zoom { Enabled = false },
                Animations = new Animations { Enabled = false },
                Background = "transparent",
                ParentHeightOffset = 0,
                FontFamily = "Roboto, sans-serif"
            },
            // ApexCharts parses series colors and cannot resolve CSS variables here.
            Colors = ["#888888", "#D98000"],
            Stroke = new Stroke { Curve = Curve.Stepline },
            Fill = new Fill { Opacity = 1d },
            Markers = new Markers { Size = 0.1, StrokeWidth = 0.1 },
            DataLabels = new DataLabels { Enabled = false },
            Legend = new Legend { Show = false },
            Tooltip = new Tooltip { X = new TooltipX { Format = "MMM dd" } },
            Grid = new Grid { Show = false, Padding = new Padding { Left = 8, Right = 12, Top = 12, Bottom = 0 } },
            Xaxis = new XAxis
            {
                Type = XAxisType.Datetime,
                Min = Timestamp(today.AddDays(-30)),
                Max = Timestamp(today.AddDays(90)),
                Labels = new XAxisLabels { Show = false },
                AxisBorder = new AxisBorder { Show = false },
                AxisTicks = new AxisTicks { Show = false },
                Tooltip = new XAxisTooltip { Enabled = false }
            },
            Yaxis = [new YAxis { Show = false }],
            Annotations = new Annotations
            {
                Xaxis = new[] { 0, 30, 60, 90 }.Select(days => new AnnotationsXAxis
                {
                    X = Timestamp(today.AddDays(days)),
                    BorderColor = days == 0 ? muted : "var(--mud-palette-divider)",
                    StrokeDashArray = days == 0 ? 0 : 3,
                    Label = new Label
                    {
                        Text = days == 0 ? "Today" : $"{days}d",
                        Orientation = Orientation.Horizontal,
                        BorderColor = "transparent",
                        OffsetX = days == 90 ? -12 : 0,
                        Style = new Style { Background = "transparent", Color = muted, FontSize = "10px" }
                    }
                }).ToList(),
                Points = model.LowestBalancePoint is TimeSeriesModel lowest
                    ? [new AnnotationsPoint
                    {
                        X = Timestamp(lowest.DateTime),
                        Y = (double?)lowest.Value,
                        SeriesIndex = 1,
                        Marker = new AnnotationMarker
                        {
                            Size = 4,
                            FillColor = lowest.Value < 0 ? "var(--mud-palette-warning)" : "var(--mud-palette-primary)",
                            StrokeColor = "var(--mud-palette-surface)",
                            StrokeWidth = 2
                        }
                    }]
                    : []
            }
        };
    }

    private static long Timestamp(DateTime date) => new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

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

    private string FormatMoney(decimal value) => MoneyFormatter.Format(value, _model?.Currency);
}