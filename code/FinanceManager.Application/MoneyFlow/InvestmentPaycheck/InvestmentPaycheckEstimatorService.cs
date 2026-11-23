using FinanceManager.Application.FinancialAccounts.Bond.Valuation;
using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;
using FinanceManager.Domain.Transactions.Bonds.Repositories;
using FinanceManager.Domain.Transactions.Cash.Repositories;

namespace FinanceManager.Application.MoneyFlow.InvestmentPaycheck;

public class InvestmentPaycheckEstimatorService(
    ICashTransactionReader cashTransactionReader,
    IFinancialLabelsRepository financialLabelsRepository,
    IInvestmentValuationService investmentValuationService,
    IBondTransactionReader bondTransactionReader,
    BondDashboardContext bondDashboardContext) : IInvestmentPaycheckEstimatorService
{
    public async Task<InvestmentPaycheckEstimate> GetEstimate(int userId, Currency currency, DateTime asOfDate, decimal annualWithdrawalRate = 0.05m, int salaryMonths = 3)
    {
        if (asOfDate > DateTime.UtcNow)
            asOfDate = DateTime.UtcNow;

        if (annualWithdrawalRate <= 0)
            annualWithdrawalRate = 0.05m;

        if (salaryMonths <= 0)
            salaryMonths = 3;

        decimal investableAssetsValue = await GetInvestableAssetsValue(userId, currency, asOfDate);
        var (averageMonthlySalary, monthsUsed) = await GetAverageSalary(userId, asOfDate, salaryMonths);

        var sustainableMonthlyPaycheck = Math.Round(investableAssetsValue * annualWithdrawalRate / 12m, 2);
        decimal? incomeReplacementRatio = null;
        if (averageMonthlySalary.HasValue && averageMonthlySalary.Value != 0)
            incomeReplacementRatio = Math.Round(sustainableMonthlyPaycheck / averageMonthlySalary.Value, 4);

        return new InvestmentPaycheckEstimate
        {
            AsOfDate = asOfDate,
            AnnualWithdrawalRate = annualWithdrawalRate,
            InvestableAssetsValue = Math.Round(investableAssetsValue, 2),
            SustainableMonthlyPaycheck = sustainableMonthlyPaycheck,
            SalaryMonthsRequested = salaryMonths,
            SalaryMonthsUsed = monthsUsed,
            AverageMonthlySalary = averageMonthlySalary,
            IncomeReplacementRatio = incomeReplacementRatio,
        };
    }

    // Bonds stay in their own currency like before (no FX on this sum); investments are valued in the requested currency.
    // Both read the owner's pooled positions, so entries and trades not linked to an account count as well.
    private async Task<decimal> GetInvestableAssetsValue(int userId, Currency currency, DateTime asOfDate)
    {
        decimal result = 0;

        var heldBonds = (await bondTransactionReader.GetPositions(userId, asOfDate)).Where(p => p.Value > 0m).ToList();
        if (heldBonds.Count > 0)
        {
            await PooledBondValuation.LoadDetailsAsync(bondDashboardContext, heldBonds);
            var date = DateOnly.FromDateTime(asOfDate);
            var valuation = new PooledBondValuation(bondDashboardContext, date);
            result += heldBonds.Sum(position => valuation.ValueOn(position, date));
        }

        result += (await investmentValuationService.GetUserValueByListingAsync(userId, currency, asOfDate)).Values.Sum();
        return result;
    }

    private async Task<(decimal? averageMonthlySalary, int monthsUsed)> GetAverageSalary(int userId, DateTime asOfDate, int salaryMonths)
    {
        var salaryLabel = await financialLabelsRepository
            .GetLabels()
            .FirstOrDefaultAsync(x => x.Name.ToLower() == "salary");

        if (salaryLabel is null)
            return (null, 0);

        DateTime firstDayOfCurrentMonth = new(asOfDate.Year, asOfDate.Month, 1, 0, 0, 0, asOfDate.Kind == DateTimeKind.Unspecified ? DateTimeKind.Utc : asOfDate.Kind);
        DateTime start = firstDayOfCurrentMonth.AddMonths(-salaryMonths);
        DateTime endExclusive = firstDayOfCurrentMonth;

        var salaryEntries = await cashTransactionReader.GetLabeledEntries(userId, start, endExclusive, labelId: salaryLabel.Id);
        if (salaryEntries.Count == 0)
            return (null, 0);

        // The salary is summed as stored (no FX), like before; a month counts once however many paychecks landed in it.
        var salary = salaryEntries.Sum(entry => entry.ValueChange);
        var salaryMonthsUsed = salaryEntries.Select(entry => (entry.PostingDate.Year, entry.PostingDate.Month)).Distinct().ToList();

        var monthsUsed = salaryMonthsUsed.Count;
        var averageMonthlySalary = Math.Round(salary / monthsUsed, 2);
        return (averageMonthlySalary, monthsUsed);
    }
}