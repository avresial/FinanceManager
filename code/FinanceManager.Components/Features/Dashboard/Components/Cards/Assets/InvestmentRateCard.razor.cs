using ApexCharts;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Dashboard.Services;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Globalization;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class InvestmentRateCard
{
    private const string _highlightColor = "#FF9800";
    private const decimal _maximumChartPercentage = 300m;
    private const string _mutedBarColor = "#5F6368";
    private const string _mutedLabelColor = "var(--mud-palette-text-secondary)";

    private static readonly string[] _singleLetterMonths =
        ["J", "F", "M", "A", "M", "J", "J", "A", "S", "O", "N", "D"];

    private readonly DateTime _asOfDate = DateTime.UtcNow;
    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;

    public List<InvestmentRate> MonthlyInvestmentRates { get; set; } = [];

    private InvestmentRate? CurrentMonthRate => MonthlyInvestmentRates.LastOrDefault();

    internal InvestmentRate? SelectedMonthRate =>
        _selectedRateIndex >= 0 && _selectedRateIndex < MonthlyInvestmentRates.Count
            ? MonthlyInvestmentRates[_selectedRateIndex]
            : null;

    private string SelectedMonthName =>
        SelectedMonthRate?.Start.ToString("MMMM", CultureInfo.InvariantCulture) ?? "Month";

    private decimal _currentMonthPercentage;
    private decimal _ytdAveragePercentage;
    private decimal? _endOfYearProjection;
    private int _selectedRateIndex;
    private List<MonthBar> _series = [];
    private ApexChartOptions<MonthBar>? _chartOptions;

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required ILogger<InvestmentRateCard> Logger { get; set; }
    [Inject] public required InvestmentRateCacheService InvestmentRateCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _currency = await SettingsService.GetCurrencyAsync();
        await LoadInvestmentRatesAsync();
    }

    private async Task LoadInvestmentRatesAsync()
    {
        _isLoading = true;
        try
        {
            MonthlyInvestmentRates.Clear();

            var user = await LoginService.GetLoggedUser();
            if (user is null) return;

            try
            {
                var context = new InvestmentRateRefreshContext
                {
                    UserId = user.UserId,
                    CurrencyId = _currency.Id,
                    EndDateTime = _asOfDate,
                };

                var snapshot = await InvestmentRateCacheService.GetSnapshotAsync(context);
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
        _selectedRateIndex = MonthlyInvestmentRates.Count - 1;
        _currentMonthPercentage = CurrentMonthRate?.GetPercentage() ?? 0m;

        var currentYear = _asOfDate.Year;
        var ytdEntries = MonthlyInvestmentRates.Where(r => r.Start.Year == currentYear).ToList();
        var salaryYtd = ytdEntries.Sum(r => r.Salary);
        var investedYtd = ytdEntries.Sum(r => r.InvestmentsChange);
        _ytdAveragePercentage = salaryYtd == 0m ? 0m : investedYtd / salaryYtd;

        var monthsElapsed = _asOfDate.Month;

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
            var pct = rate is not null ? Math.Min(rate.GetPercentage() * 100m, _maximumChartPercentage) : 0m;
            bars.Add(new MonthBar(label, pct, IsSelected: i == _selectedRateIndex, Key: $"{i}-{label}"));
        }

        _series = bars;

        var labelColors = bars
            .Select(b => b.IsSelected ? _highlightColor : _mutedLabelColor)
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
                        Label = new Label
                        {
                            Text = FormatAveragePercentage(_ytdAveragePercentage),
                            Position = LabelPosition.Right,
                            BorderColor = "transparent",
                            Style = new Style
                            {
                                Background = "transparent",
                                Color = _mutedLabelColor,
                                FontSize = "11px",
                            },
                        },
                    },
                ],
            };
        }
    }

    private void OnBarSelected(SelectedData<MonthBar> selection)
    {
        if (!SelectMonth(selection.DataPointIndex)) return;
        BuildChart();
    }

    internal bool SelectMonth(int index)
    {
        if (index < 0 || index >= MonthlyInvestmentRates.Count) return false;
        _selectedRateIndex = index;
        return true;
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

    internal record MonthBar(string Label, decimal Percentage, bool IsSelected, string Key);
}