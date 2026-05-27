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
    private const string _mutedBarColor = "#33FFFFFF";
    private const string _highlightBarColor = "#FF8F00";

    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;

    public List<InvestmentRate> InvestmentRates { get; set; } = [];
    public List<InvestmentRate> MonthlyInvestmentRates { get; set; } = [];

    private InvestmentRate? LatestInvestmentRate => InvestmentRates.FirstOrDefault(x => x.Salary != 0);
    private InvestmentRate? CurrentMonthRate => MonthlyInvestmentRates.LastOrDefault();

    private decimal _currentMonthPercentage;
    private decimal _ytdAveragePercentage;
    private decimal? _endOfYearProjection;
    private List<ChartSeries<double>> _chartSeries = [];
    private ChartOptions _chartOptions = new();
    private string[] _chartLabels = [];

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime StartDateTime { get; set; }
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;

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
        var data = MonthlyInvestmentRates
            .Select(r => (double)(r.GetPercentage() * 100m))
            .ToArray();

        if (data.Length == 0)
            data = new double[12];

        _chartLabels = [.. MonthlyInvestmentRates.Select(r => r.Start.ToString("MMM"))];
        if (_chartLabels.Length == 0)
            _chartLabels = new string[12];

        _chartSeries =
        [
            new ChartSeries<double> { Name = "Rate", Data = data },
        ];

        var palette = new string[data.Length];
        for (int i = 0; i < data.Length; i++)
            palette[i] = _mutedBarColor;
        if (data.Length > 0 && CurrentMonthRate is { Salary: not 0 })
            palette[^1] = _highlightBarColor;

        _chartOptions = new ChartOptions
        {
            ChartPalette = palette,
            ShowLegend = false,
            ShowToolTips = false,
        };
    }

    private string FormatPercentage(decimal value) => $"{value * 100m:0.00}%";
    private string FormatAveragePercentage(decimal value) => $"{value * 100m:0.0}%";
    private string FormatAmount(decimal value) => $"{value:0.00} {_currency.ShortName}";
    private string FormatProjection() => _endOfYearProjection is null
        ? "—"
        : $"{_endOfYearProjection.Value:N0} {_currency.ShortName}";
}