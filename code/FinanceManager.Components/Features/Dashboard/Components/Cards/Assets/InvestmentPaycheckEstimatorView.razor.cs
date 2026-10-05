using FinanceManager.Components.Features.Dashboard.Models;
using FinanceManager.Components.Shared.Helpers;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using Microsoft.AspNetCore.Components;

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class InvestmentPaycheckEstimatorView
{
    private const decimal _defaultRate = 0.04m;
    private const decimal _rateMin = 0.02m;
    private const decimal _rateMax = 0.08m;
    private const decimal _rateStep = 0.001m;

    private readonly RatePreset[] _presets =
    [
        new(0.03m, "Conservative"),
        new(0.04m, "Standard"),
        new(0.05m, "Aggressive"),
    ];

    internal decimal _annualWithdrawalRate = _defaultRate;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public int SalaryMonths { get; set; } = 3;
    [Parameter] public Currency Currency { get; set; } = DefaultCurrency.PLN;
    [Parameter] public InvestmentPaycheckSourceModel? Source { get; set; }
    [Parameter] public bool IsLoading { get; set; }
    [Parameter] public bool HasError { get; set; }

    internal decimal MonthlyPaycheck => Math.Round((Source?.InvestableAssetsValue ?? 0m) * _annualWithdrawalRate / 12m, 2);

    internal decimal? ReplacementRatio
    {
        get
        {
            if (Source?.AverageMonthlySalary is not { } averageSalary || averageSalary == 0m)
                return null;
            return Math.Round(MonthlyPaycheck / averageSalary, 4);
        }
    }

    internal void OnRateChanged(decimal value) => _annualWithdrawalRate = value;

    internal void OnPresetSelected(decimal rate) => _annualWithdrawalRate = rate;

    private string FormatCurrency(decimal value) => MoneyFormatter.Format(value, Currency.ShortName);

    private string FormatMonthly(decimal value) => MoneyFormatter.FormatNumber(value);

    private static string FormatRate(decimal value) => $"{value * 100m:0.0}%";

    private static string FormatReplacement(decimal? value) => value.HasValue ? $"{value.Value * 100m:0.00}%" : "—";

    private readonly record struct RatePreset(decimal Rate, string Label);
}