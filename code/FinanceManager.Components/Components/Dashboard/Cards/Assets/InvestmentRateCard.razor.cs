using ApexCharts;
using FinanceManager.Components.Models;
using FinanceManager.Components.Services;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;

namespace FinanceManager.Components.Components.Dashboard.Cards.Assets;

public partial class InvestmentRateCard
{
    private const string _highlightColor = "#FF9800";
    private const string _mutedBarColor = "#5F6368";
    private const string _mutedLabelColor = "var(--mud-palette-text-secondary)";
    private const int _chromeHeightPx = 240;
    private const int _minChartHeightPx = 110;

    private static readonly string[] _singleLetterMonths =
        ["J", "F", "M", "A", "M", "J", "J", "A", "S", "O", "N", "D"];

    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;

    public List<InvestmentRate> InvestmentRates { get; set; } = [];
    public List<InvestmentRate> MonthlyInvestmentRates { get; set; } = [];

    private InvestmentRate? LatestInvestmentRate => InvestmentRates.FirstOrDefault(x => x.Salary != 0);
    private InvestmentRate? CurrentMonthRate => MonthlyInvestmentRates.LastOrDefault();

    private decimal _currentMonthPercentage;
    private decimal _ytdAveragePercentage;
    private decimal? _endOfYearProjection;
    private List<MonthBar> _series = [];
    private ApexChartOptions<MonthBar>? _chartOptions;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

    private string ChartHeightPx => $"{Math.Max(_minChartHeightPx, ParseHeightPx(Height) - _chromeHeightPx)}px";

    [Inject] public required ILogger<InvestmentRateCard> Logger { get; set; }
    [Inject] public required AssetsPageCardsCacheService AssetsPageCardsCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override void OnInitialized()
    {
        _currency = SettingsService.GetCurrency();
    }

    protected override async Task OnParametersSetAsync()
    {
        _isLoading = true;
        try
        {
            InvestmentRates.Clear();
            MonthlyInvestmentRates.Clear();

            var user = await LoginService.GetLoggedUser();
            if (user is null) return;

            try
            {
                var context = new AssetsPageCardsRefreshContext
                {
                    UserId = user.UserId,
                    CurrencyId = _currency.Id,
                    StartDateTime = StartDateTime,
                    EndDateTime = EndDateTime,
                };

                var snapshot = await AssetsPageCardsCacheService.GetSnapshotAsync(context);
                InvestmentRates = [.. snapshot.InvestmentRates];
                MonthlyInvestmentRates = [.. snapshot.MonthlyInvestmentRates];

                BuildDerivedState();
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error while getting investment rate");
            }
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void BuildDerivedState()
    {
        _currentMonthPercentage = CurrentMonthRate?.GetPercentage() ?? 0m;

        var currentYear = EndDateTime.Year;
        var ytdEntries = MonthlyInvestmentRates
            .Where(r => r.Start.Year == currentYear && r.Salary != 0)
            .ToList();

        _ytdAveragePercentage = ytdEntries.Count == 0
            ? 0m
            : ytdEntries.Average(r => r.GetPercentage());

        var monthsElapsed = EndDateTime.Month;
        var investedYtd = MonthlyInvestmentRates
            .Where(r => r.Start.Year == currentYear)
            .Sum(r => r.InvestmentsChange);

        _endOfYearProjection = monthsElapsed == 0 || investedYtd == 0
            ? null
            : investedYtd / monthsElapsed * 12m;

        BuildChart();
    }

    private void BuildChart()
    {
        var bars = new List<MonthBar>(12);
        for (int i = 0; i < 12; i++)
        {
            var rate = i < MonthlyInvestmentRates.Count ? MonthlyInvestmentRates[i] : null;
            var monthIndex = rate is not null ? rate.Start.Month - 1 : i;
            var label = _singleLetterMonths[monthIndex];
            var pct = rate is not null ? (decimal)rate.GetPercentage() * 100m : 0m;
            bars.Add(new MonthBar(label, pct, IsCurrent: i == 11, Key: $"{i}-{label}"));
        }

        _series = bars;

        var labelColors = bars
            .Select(b => b.IsCurrent ? _highlightColor : _mutedLabelColor)
            .ToArray();

        _chartOptions = new ApexChartOptions<MonthBar>
        {
            Chart = new Chart
            {
                Toolbar = new Toolbar { Show = false },
                Animations = new Animations { Enabled = false },
                FontFamily = "Roboto, sans-serif",
                Background = "transparent",
                ParentHeightOffset = 0,
                Sparkline = new ChartSparkline { Enabled = false },
            },
            PlotOptions = new PlotOptions
            {
                Bar = new PlotOptionsBar
                {
                    BorderRadius = 6,
                    BorderRadiusApplication = BorderRadiusApplication.End,
                    ColumnWidth = "78%",
                },
            },
            DataLabels = new DataLabels { Enabled = false },
            Grid = new Grid
            {
                Show = false,
                Padding = new Padding { Top = 0, Right = 0, Bottom = 0, Left = 0 },
            },
            Legend = new Legend { Show = false },
            Tooltip = new Tooltip { Enabled = false },
            Stroke = new Stroke { Show = false },
            Xaxis = new XAxis
            {
                AxisBorder = new AxisBorder { Show = false },
                AxisTicks = new AxisTicks { Show = false },
                Labels = new XAxisLabels
                {
                    Style = new AxisLabelStyle
                    {
                        FontSize = "11px",
                        Colors = new ApexCharts.Color(labelColors),
                    },
                },
                Tooltip = new XAxisTooltip { Enabled = false },
            },
            Yaxis = [new YAxis { Show = false }],
        };

        if (_ytdAveragePercentage != 0m)
        {
            _chartOptions.Annotations = new Annotations
            {
                Yaxis =
                [
                    new AnnotationsYAxis
                    {
                        Y = _ytdAveragePercentage * 100m,
                        BorderColor = _mutedLabelColor,
                        StrokeDashArray = 4,
                        BorderWidth = 1,
                    },
                ],
            };
        }
    }

    private static int ParseHeightPx(string height)
    {
        if (height.EndsWith("px", StringComparison.Ordinal)
            && int.TryParse(height.AsSpan(0, height.Length - 2), out var px))
            return px;
        return 380;
    }

    private static string FormatRateNumber(decimal value) => $"{value * 100m:0.00}";
    private static string FormatAveragePercentage(decimal value) => $"{value * 100m:0.0}%";
    private string FormatAmount(decimal value) => $"{value:N2} {_currency.ShortName}";
    private string FormatChange(decimal value)
    {
        var sign = value > 0 ? "+" : value < 0 ? "-" : string.Empty;
        return $"{sign}{Math.Abs(value):N2} {_currency.ShortName}";
    }
    private string FormatProjection() => _endOfYearProjection is null
        ? "—"
        : $"{_endOfYearProjection.Value:N0} {_currency.ShortName}";

    internal record MonthBar(string Label, decimal Percentage, bool IsCurrent, string Key);
}