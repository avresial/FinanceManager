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

namespace FinanceManager.Components.Features.Dashboard.Components.Cards.Assets;

public partial class InvestmentPaycheckEstimatorCard
{
    private const decimal _defaultRate = 0.04m;
    private const decimal _rateMin = 0.02m;
    private const decimal _rateMax = 0.08m;
    private const decimal _rateStep = 0.001m;
    private readonly DateTime _asOfDate = DateTime.UtcNow;

    private readonly RatePreset[] _presets =
    [
        new(0.03m, "Conservative"),
        new(0.04m, "Standard"),
        new(0.05m, "Aggressive"),
    ];

    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;
    private InvestmentPaycheckEstimate _estimate = new() { AnnualWithdrawalRate = _defaultRate, SalaryMonthsRequested = 3 };
    private decimal _annualWithdrawalRate = _defaultRate;
    private CancellationTokenSource? _persistCts;

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public int SalaryMonths { get; set; } = 3;

    [Inject] public required ILogger<InvestmentPaycheckEstimatorCard> Logger { get; set; }
    [Inject] public required InvestmentPaycheckEstimateCacheService InvestmentPaycheckEstimateCacheService { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    private decimal MonthlyPaycheck => _estimate.InvestableAssetsValue * _annualWithdrawalRate / 12m;

    private decimal? ReplacementRatio
    {
        get
        {
            if (_estimate.AverageMonthlySalary is not { } avg || avg == 0m)
                return null;
            return MonthlyPaycheck / avg;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        _currency = await SettingsService.GetCurrencyAsync();
        await RefreshEstimate(showLoading: true);
    }

    // showLoading is suppressed for slider/preset changes: the paycheck is recomputed
    // client-side from the (rate-independent) estimate, so the value is already correct
    // the instant the rate moves. Flashing the spinner there only hides an unchanged value.
    private async Task RefreshEstimate(bool showLoading)
    {
        if (showLoading)
        {
            _isLoading = true;
            StateHasChanged();
        }

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _estimate = new InvestmentPaycheckEstimate { AnnualWithdrawalRate = _defaultRate, SalaryMonthsRequested = SalaryMonths };
                return;
            }

            var withdrawalRate = Math.Round(_annualWithdrawalRate, 4);
            var context = new InvestmentPaycheckEstimateRefreshContext
            {
                UserId = user.UserId,
                CurrencyId = _currency.Id,
                EndDateTime = _asOfDate,
                WithdrawalRate = withdrawalRate,
                SalaryMonths = SalaryMonths,
            };

            var snapshot = await InvestmentPaycheckEstimateCacheService.GetSnapshotAsync(context);
            _estimate = snapshot.Estimate;
            _annualWithdrawalRate = _estimate.AnnualWithdrawalRate;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while getting investment paycheck estimate");
            _estimate = new InvestmentPaycheckEstimate { AnnualWithdrawalRate = _defaultRate, SalaryMonthsRequested = SalaryMonths };
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private void OnRateChanged(decimal value)
    {
        _annualWithdrawalRate = value;
        SchedulePersist();
    }

    private void OnPresetSelected(decimal rate)
    {
        _annualWithdrawalRate = rate;
        SchedulePersist();
    }

    private void SchedulePersist()
    {
        _persistCts?.Cancel();
        _persistCts = new CancellationTokenSource();
        var token = _persistCts.Token;

        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(350), token);
                if (token.IsCancellationRequested)
                    return;
                await InvokeAsync(() => RefreshEstimate(showLoading: false));
            }
            catch (TaskCanceledException)
            {
            }
        });
    }

    private string FormatCurrency(decimal value) => $"{value:0.00} {_currency.ShortName}";

    private string FormatMonthly(decimal value) => value.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatNumber(decimal value, int decimals)
        => value.ToString($"N{decimals}", System.Globalization.CultureInfo.InvariantCulture);

    private static string FormatRate(decimal value) => $"{value * 100m:0.0}%";

    private static string FormatReplacement(decimal? value) => value.HasValue ? $"{value.Value * 100m:0.00}%" : "—";

    private readonly record struct RatePreset(decimal Rate, string Label);
}