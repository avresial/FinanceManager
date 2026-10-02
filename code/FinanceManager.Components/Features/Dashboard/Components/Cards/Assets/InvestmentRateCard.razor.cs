using ApexCharts;
using FinanceManager.Application.Identity.Users;
using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Features.Identity.Services;
using FinanceManager.Components.Features.MoneyFlow.HttpClients;
using FinanceManager.Components.Shared.Models;
using FinanceManager.Components.Shared.Services;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Services;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.MoneyFlow.Entities;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using MudBlazor;
using System.Globalization;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class InvestmentRateCard : IDisposable
{
    private const int _horizonMonths = 12;
    private const string _highlightColor = "#FF9800";
    private const decimal _maximumChartPercentage = 300m;
    private const string _mutedBarColor = "#5F6368";
    private const string _mutedLabelColor = "var(--mud-palette-text-secondary)";

    private static readonly string[] _singleLetterMonths =
        ["J", "F", "M", "A", "M", "J", "J", "A", "S", "O", "N", "D"];

    internal DateTime AsOfDate { get; set; } = DateTime.UtcNow;

    private bool _isLoading;
    private bool _hasError;
    private Currency _currency = DefaultCurrency.PLN;
    private readonly RefreshVersionGate _gate = new();
    private (int UserId, int CurrencyId, int Year, int Month)? _scope;
    private bool _hasDisplayedModel;

    public List<InvestmentRate> MonthlyInvestmentRates { get; set; } = [];

    private InvestmentRate? CurrentMonthRate => MonthlyInvestmentRates.LastOrDefault();

    internal InvestmentRate? SelectedMonthRate =>
        _selectedRateIndex >= 0 && _selectedRateIndex < MonthlyInvestmentRates.Count
            ? MonthlyInvestmentRates[_selectedRateIndex]
            : null;

    internal decimal? CurrentMonthPercentage => _currentMonthPercentage;
    internal decimal? YtdAveragePercentage => _ytdAveragePercentage;
    internal decimal? EndOfYearProjection => _endOfYearProjection;
    internal bool IsLoading => _isLoading;
    internal IReadOnlyList<MonthBar> Series => _series;

    private string SelectedMonthName =>
        SelectedMonthRate?.Start.ToString("MMMM", CultureInfo.InvariantCulture) ?? "Month";

    private decimal? _currentMonthPercentage;
    private decimal? _ytdAveragePercentage;
    private decimal? _endOfYearProjection;
    private int _selectedRateIndex;
    private int _chartVersion;
    private List<MonthBar> _series = [];
    private ApexChartOptions<MonthBar>? _chartOptions;

    [Parameter] public string Height { get; set; } = "300px";

    [Inject] public required ILogger<InvestmentRateCard> Logger { get; set; }
    [Inject] public required MoneyFlowHttpClient MoneyFlowHttpClient { get; set; }
    [Inject] public required ISnapshotRefreshCoordinator Coordinator { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        await LoadInvestmentRatesAsync();
    }

    internal async Task LoadInvestmentRatesAsync()
    {
        var version = _gate.Claim();
        var asOf = AsOfDate;
        var month = new DateOnly(asOf.Year, asOf.Month, 1);
        _isLoading = !_hasDisplayedModel;
        if (_scope is { } knownScope && (knownScope.Year != month.Year || knownScope.Month != month.Month))
            ClearDisplayedModel();
        StateHasChanged();

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (!_gate.IsCurrent(version)) return;
            if (user is null)
            {
                ClearDisplayedModel();
                _scope = null;
                _isLoading = false;
                _hasError = true;
                StateHasChanged();
                return;
            }
            var userId = user.UserId;

            if (_scope is { } current && (current.UserId != userId || current.Year != month.Year || current.Month != month.Month))
            {
                ClearDisplayedModel();
                StateHasChanged();
            }

            var resolvedCurrency = await SettingsService.GetCurrencyAsync();
            if (!_gate.IsCurrent(version)) return;
            var currency = resolvedCurrency with { };
            var currencyId = currency.Id;

            var scope = (userId, currencyId, month.Year, month.Month);
            var sameScope = _scope == scope && _hasDisplayedModel;
            if (_scope != scope)
            {
                ClearDisplayedModel();
                StateHasChanged();
            }
            _scope = scope;
            _currency = currency;

            var firstMonth = month.AddMonths(-(_horizonMonths - 1));
            var key = $"investment-rate-card:{userId}:{currencyId}:{_horizonMonths}";
            var result = await Coordinator.RunAsync(new SnapshotRefreshRequest<InvestmentRateCardSnapshot, InvestmentRateCardModel>
            {
                Key = key,
                Gate = _gate,
                ClaimedVersion = version,
                ToModel = snapshot => snapshot.SchemaVersion == SnapshotBase.CurrentSchemaVersion
                    && snapshot.UserId == userId && snapshot.CurrencyId == currencyId
                    && snapshot.HorizonMonths == _horizonMonths && snapshot.AsOfMonth == month
                    && IsExpectedMonths(snapshot.Model, firstMonth)
                    ? snapshot.Model : null,
                FetchAsync = async () => await FetchMonthlyRatesAsync(userId, currency, asOf, firstMonth),
                ToSnapshot = model => new InvestmentRateCardSnapshot
                {
                    SchemaVersion = SnapshotBase.CurrentSchemaVersion,
                    UserId = userId,
                    CurrencyId = currencyId,
                    HorizonMonths = _horizonMonths,
                    AsOfMonth = month,
                    AsOfDateTime = asOf,
                    Model = model,
                },
                ContentComparer = InvestmentRateCardModelComparer.Instance,
                OnSnapshotPainted = model => ShowModel(model),
                OnSnapshotMissing = () =>
                {
                    if (!sameScope)
                    {
                        _isLoading = true;
                        StateHasChanged();
                    }
                    return Task.CompletedTask;
                },
                OnRefreshed = model => ShowModel(model),
            });
            if (!_gate.IsCurrent(version)) return;
            _isLoading = false;
            if (result.IsBlockingFailure && !_hasDisplayedModel)
            {
                _hasError = true;
                Logger.LogWarning("Could not load investment rates for the current scope.");
            }
            StateHasChanged();
        }
        catch (Exception ex)
        {
            if (!_gate.IsCurrent(version)) return;
            _isLoading = false;
            _hasError = !_hasDisplayedModel;
            Logger.LogError(ex, "Error while getting investment rate");
            StateHasChanged();
        }
    }

    private async Task<InvestmentRateCardModel> FetchMonthlyRatesAsync(int userId, Currency currency, DateTime asOf, DateOnly firstMonth)
    {
        var requests = Enumerable.Range(0, _horizonMonths).Select(async offset =>
        {
            var month = firstMonth.AddMonths(offset);
            var start = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
            var end = start.AddMonths(1).AddTicks(-1);
            if (offset == _horizonMonths - 1 && end > asOf) end = asOf;
            var results = await MoneyFlowHttpClient.GetInvestmentRate(userId, currency, start, end).ToListAsync();
            var rate = results.FirstOrDefault();
            return new InvestmentRateMonthModel(month, rate?.Salary ?? 0m, rate?.InvestmentsChange ?? 0m);
        });

        return new InvestmentRateCardModel(await Task.WhenAll(requests));
    }

    private static bool IsExpectedMonths(InvestmentRateCardModel? model, DateOnly firstMonth) =>
        model?.Months is { Count: _horizonMonths } months
        && months.Select((rate, index) => rate is not null && rate.Month == firstMonth.AddMonths(index)).All(matches => matches);

    private Task ShowModel(InvestmentRateCardModel model)
    {
        var selectedMonth = SelectedMonthRate is { } selected
            ? new DateOnly(selected.Start.Year, selected.Start.Month, 1)
            : (DateOnly?)null;
        MonthlyInvestmentRates = [.. model.Months.Select(rate => new InvestmentRate
        {
            Start = new DateTime(rate.Month.Year, rate.Month.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            Salary = rate.Salary,
            InvestmentsChange = rate.InvestmentsChange,
        })];
        _selectedRateIndex = selectedMonth is { } selectedDate
            ? MonthlyInvestmentRates.FindIndex(rate => rate.Start.Year == selectedDate.Year && rate.Start.Month == selectedDate.Month)
            : -1;
        if (_selectedRateIndex < 0) _selectedRateIndex = MonthlyInvestmentRates.Count - 1;
        _hasDisplayedModel = true;
        _isLoading = false;
        _hasError = false;
        _chartVersion++;
        BuildDerivedState();
        StateHasChanged();
        return Task.CompletedTask;
    }

    private void ClearDisplayedModel()
    {
        MonthlyInvestmentRates = [];
        _selectedRateIndex = -1;
        _hasDisplayedModel = false;
        _hasError = false;
        _isLoading = true;
        BuildDerivedState();
    }

    public void Dispose()
    {
        _gate.Claim();
    }

    private sealed class InvestmentRateCardModelComparer : IEqualityComparer<InvestmentRateCardModel>
    {
        public static InvestmentRateCardModelComparer Instance { get; } = new();

        public bool Equals(InvestmentRateCardModel? x, InvestmentRateCardModel? y) =>
            ReferenceEquals(x, y) || x is not null && y is not null && x.Months.SequenceEqual(y.Months);

        public int GetHashCode(InvestmentRateCardModel obj)
        {
            var hash = new HashCode();
            foreach (var month in obj.Months) hash.Add(month);
            return hash.ToHashCode();
        }
    }

    internal void BuildDerivedState()
    {
        _currentMonthPercentage = CurrentMonthRate?.GetPercentage();

        var currentYear = AsOfDate.Year;
        var ytdEntries = MonthlyInvestmentRates.Where(r => r.Start.Year == currentYear).ToList();

        // Months without a salary have no rate, so they must stay out of the average entirely —
        // counting their investments against the other months' salary would overstate it.
        var ratedYtdEntries = ytdEntries.Where(r => r.HasRate).ToList();
        var salaryYtd = ratedYtdEntries.Sum(r => r.Salary);
        _ytdAveragePercentage = salaryYtd == 0m ? null : ratedYtdEntries.Sum(r => r.InvestmentsChange) / salaryYtd;

        var investedYtd = ytdEntries.Sum(r => r.InvestmentsChange);
        var monthsElapsed = AsOfDate.Month;

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
            // A month with no salary has no rate to plot — leave the bar empty rather than drawing a 0 %.
            var pct = rate?.GetPercentage() is decimal percentage
                ? Math.Min(percentage * 100m, _maximumChartPercentage)
                : (decimal?)null;
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

        if (_ytdAveragePercentage is decimal average && average != 0m)
        {
            _chartOptions.Annotations = new Annotations
            {
                Yaxis =
                [
                    new AnnotationsYAxis
                    {
                        Y = average * 100m,
                        BorderColor = _mutedLabelColor,
                        StrokeDashArray = 4,
                        BorderWidth = 1,
                        Label = new Label
                        {
                            Text = FormatAveragePercentage(average),
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
    private static string FormatAveragePercentage(decimal? value) => value is decimal average ? $"{average * 100m:0.0}%" : "—";
    private string FormatAmount(decimal value) => $"{value:N2} {_currency.ShortName}";
    private string FormatChange(decimal value)
    {
        var sign = value > 0 ? "+" : value < 0 ? "-" : string.Empty;
        return $"{sign}{Math.Abs(value):N2} {_currency.ShortName}";
    }
    private string FormatProjection() => _endOfYearProjection is null
        ? "—"
        : $"{_endOfYearProjection.Value:N0} {_currency.ShortName}";

    internal record MonthBar(string Label, decimal? Percentage, bool IsSelected, string Key);
}