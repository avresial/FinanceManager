using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class InvestmentPaycheckEstimatorService(
    IFinancialAccountRepository financialAccountRepository,
    IFinancialLabelsRepository financialLabelsRepository,
    IStockPriceProvider stockPriceProvider,
    IBondDetailsRepository bondDetailsRepository) : IInvestmentPaycheckEstimatorService
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

    private async Task<decimal> GetInvestableAssetsValue(int userId, Currency currency, DateTime asOfDate)
    {
        decimal result = 0;
        var bondDetails = await bondDetailsRepository.GetAllAsync().ToDictionaryAsync(x => x.Id);

        await foreach (var account in financialAccountRepository.GetAccounts<BondAccount>(userId, asOfDate.Date, asOfDate))
        {
            foreach (var detailsId in account.GetStoredBondsIds())
            {
                var newestEntry = account.GetThisOrNextOlder(asOfDate, detailsId);
                if (newestEntry is null)
                    continue;
                if (!bondDetails.TryGetValue(detailsId, out var details))
                    continue;

                result += newestEntry.GetPriceAt(DateOnly.FromDateTime(asOfDate), details);
            }
        }

        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, asOfDate.Date, asOfDate))
        {
            foreach (var ticker in account.GetStoredTickers())
            {
                var newestEntry = account.GetThisOrNextOlder(asOfDate, ticker);
                if (newestEntry is null)
                    continue;

                decimal pricePerUnit = await stockPriceProvider.GetPricePerUnitAsync(ticker, currency, asOfDate);
                result += newestEntry.Value * pricePerUnit;
            }
        }

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

        decimal salary = 0;
        HashSet<(int Year, int Month)> salaryMonthsUsed = [];

        await foreach (CurrencyAccount account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, endExclusive))
        {
            if (account.Entries is null || account.Entries.Count == 0)
                continue;

            var salaryEntries = account.Entries
                .Where(x => x.PostingDate >= start && x.PostingDate < endExclusive && x.Labels is not null && x.Labels.Any(y => y.Id == salaryLabel.Id));

            salary += salaryEntries.Sum(x => x.ValueChange);

            foreach (var entry in salaryEntries)
            {
                salaryMonthsUsed.Add((entry.PostingDate.Year, entry.PostingDate.Month));
            }
        }

        if (salaryMonthsUsed.Count == 0)
            return (null, 0);

        var monthsUsed = salaryMonthsUsed.Count;
        var averageMonthlySalary = Math.Round(salary / monthsUsed, 2);
        return (averageMonthlySalary, monthsUsed);
    }
}