using ApexCharts;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Helpers;
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
    private int _loadRequestVersion;
    private int _horizonDays = CashFlowForecastHorizons.NinetyDays;
    private string _currency = "PLN";
    private bool _isLoading = true;
    private bool _hasError;

    [Inject] public required CashFlowForecastHttpClient CashFlowForecastHttpClient { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
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
        var requestVersion = Interlocked.Increment(ref _loadRequestVersion);
        var horizonDays = _horizonDays;
        var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        var previousRequest = Interlocked.Exchange(ref _loadCancellationTokenSource, cancellationTokenSource);
        previousRequest?.Cancel();
        previousRequest?.Dispose();

        _isLoading = true;
        _hasError = false;

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (requestVersion != _loadRequestVersion) return;

            if (user is null)
            {
                _forecast = null;
                return;
            }

            var currency = await SettingsService.GetCurrencyAsync();
            if (requestVersion != _loadRequestVersion) return;

            var forecast = await CashFlowForecastHttpClient.GetAsync(
                user.UserId,
                currency.Id,
                horizonDays,
                cancellationToken);
            if (requestVersion != _loadRequestVersion) return;

            _currency = currency.ShortName;
            _forecast = forecast;
            _hasError = _forecast is null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (requestVersion != _loadRequestVersion) return;

            _forecast = null;
            _hasError = true;
            Logger.LogError(ex, "Unable to load cash flow forecast page.");
        }
        finally
        {
            if (requestVersion == _loadRequestVersion)
                _isLoading = false;

            if (ReferenceEquals(
                    Interlocked.CompareExchange(ref _loadCancellationTokenSource, null, cancellationTokenSource),
                    cancellationTokenSource))
            {
                cancellationTokenSource.Dispose();
            }
        }
    }

    public void Dispose()
    {
        Interlocked.Increment(ref _loadRequestVersion);
        var request = Interlocked.Exchange(ref _loadCancellationTokenSource, null);
        request?.Cancel();
        request?.Dispose();
    }

    private string FormatAmount(decimal amount) => $"{(amount >= 0 ? "+" : string.Empty)}{amount:N2} {_currency}";

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
        Stroke = new Stroke { Curve = Curve.Smooth, Width = 2, LineCap = LineCap.Round },
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