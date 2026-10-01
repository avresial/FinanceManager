using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Components.Features.Dashboard.Models;

/// <summary>Only the source facts that determine the estimator card's non-slider content.</summary>
public sealed record InvestmentPaycheckSourceModel(
    decimal InvestableAssetsValue,
    int SalaryMonthsRequested,
    int SalaryMonthsUsed,
    decimal? AverageMonthlySalary)
{
    public static InvestmentPaycheckSourceModel FromEstimate(InvestmentPaycheckEstimate estimate) => new(
        estimate.InvestableAssetsValue,
        estimate.SalaryMonthsRequested,
        estimate.SalaryMonthsUsed,
        estimate.AverageMonthlySalary);
}
