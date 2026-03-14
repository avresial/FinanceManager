using FinanceManager.Components.HttpClients;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Services;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace FinanceManager.Components.Components.Dashboard.Cards.Assets;

public partial class InvestmentPaycheckEstimatorCard
{
    private bool _isLoading;
    private Currency _currency = DefaultCurrency.PLN;
    private InvestmentPaycheckEstimate _estimate = new() { AnnualWithdrawalRate = 0.05m, SalaryMonthsRequested = 3 };
    private decimal? _withdrawalRatePercent = 0.05m;
    private bool ShowRecalculateButton => !_isLoading && Math.Round(_withdrawalRatePercent ?? 0.05m, 4) != Math.Round(_estimate.AnnualWithdrawalRate, 4);

    [Parameter] public string Height { get; set; } = "300px";
    [Parameter] public DateTime EndDateTime { get; set; } = DateTime.UtcNow;
    [Parameter] public int SalaryMonths { get; set; } = 3;

    [Inject] public required ILogger<InvestmentPaycheckEstimatorCard> Logger { get; set; }
    [Inject] public required AssetsHttpClient AssetsHttpClient { get; set; }
    [Inject] public required ISettingsService SettingsService { get; set; }
    [Inject] public required ILoginService LoginService { get; set; }

    protected override async Task OnInitializedAsync()
    {
        _currency = SettingsService.GetCurrency();
        await Task.CompletedTask;
    }

    protected override async Task OnParametersSetAsync()
    {
        await RefreshEstimate();
    }

    private async Task RefreshEstimate()
    {
        _isLoading = true;
        StateHasChanged();

        try
        {
            var user = await LoginService.GetLoggedUser();
            if (user is null)
            {
                _estimate = new InvestmentPaycheckEstimate { AnnualWithdrawalRate = 0.05m, SalaryMonthsRequested = SalaryMonths };
                return;
            }

            decimal withdrawalRate = Math.Round(_withdrawalRatePercent ?? 0.05m, 4);
            _estimate = await AssetsHttpClient.GetInvestmentPaycheckEstimate(user.UserId, _currency, EndDateTime, withdrawalRate, SalaryMonths);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error while getting investment paycheck estimate");
            _estimate = new InvestmentPaycheckEstimate { AnnualWithdrawalRate = 0.05m, SalaryMonthsRequested = SalaryMonths };
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private string FormatCurrency(decimal value) => $"{value:0.00} {_currency.ShortName}";

    private string FormatCurrency(decimal? value) => value.HasValue ? FormatCurrency(value.Value) : "No data";

    private static string FormatRatio(decimal? value) => value.HasValue ? $"{value.Value * 100m:0.00}%" : "No data";

    private string GetSalaryMessage()
    {
        if (_estimate.HasPartialSalaryHistory)
            return $"Salary baseline uses {_estimate.SalaryMonthsUsed} month(s) because fewer than {_estimate.SalaryMonthsRequested} salary months were found.";

        return "Salary baseline uses the full requested history window.";
    }
}