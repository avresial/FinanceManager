using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
using FinanceManager.Domain.FinancialAccounts.Shared.Entities;
using FinanceManager.Domain.FinancialAccounts.Shared.Repositories;
using FinanceManager.Domain.Identity.Repositories;
using FinanceManager.Domain.Identity.Services;
using FinanceManager.Domain.Labels.Repositories;
using FinanceManager.Domain.MoneyFlow.Entities;
using FinanceManager.Domain.MoneyFlow.Services;

namespace FinanceManager.Application.MoneyFlow.InvestmentRate;

public class InvestmentRateService(IFinancialAccountRepository financialAccountRepository, IFinancialLabelsRepository financialLabelsRepository,
IInvestmentValuationService investmentValuationService) : IInvestmentRateService
{
    private const int _salaryLookbackMonths = 12;

    public async IAsyncEnumerable<Domain.MoneyFlow.Entities.InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end)
    {
        var labels = await financialLabelsRepository.GetLabels().ToListAsync();
        var salaryLabel = labels.Single(x => x.Name.ToLower() == "salary");

        Currency currency = DefaultCurrency.PLN; // TODO: use user currency settings

        decimal salary = 0;
        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
            salary += SumSalary(account, salaryLabel);

        // Salaries and investment purchases regularly land in different months, so a window without a
        // salary entry still needs a denominator — use the most recent salary before the window.
        if (salary == 0)
            salary = await GetMostRecentSalaryBefore(userId, start, salaryLabel);

        List<int> investmentAccountIds = [];
        await foreach (var account in financialAccountRepository.GetAccounts<InvestmentAccount>(userId, start, end))
            investmentAccountIds.Add(account.AccountId);

        decimal investmentsChange = 0;
        if (investmentAccountIds.Count > 0)
        {
            // Batched point valuation: one transactions query per as-of date, each distinct listing
            // priced once across all accounts. The change nets across accounts, so the per-account
            // breakdown is not needed here — summing the batched result matches the per-account loop.
            var startValues = await investmentValuationService.GetAccountValueAsync(investmentAccountIds, currency, start);
            var endValues = await investmentValuationService.GetAccountValueAsync(investmentAccountIds, currency, end);
            investmentsChange = endValues.Values.Sum() - startValues.Values.Sum();
        }

        if (salary == 0 && investmentsChange == 0) yield break;

        yield return new()
        {
            Start = start,
            End = end,
            Salary = salary,
            InvestmentsChange = investmentsChange
        };
    }

    private async Task<decimal> GetMostRecentSalaryBefore(int userId, DateTime start, FinancialLabel salaryLabel)
    {
        List<CurrencyAccountEntry> salaryEntries = [];
        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start.AddMonths(-_salaryLookbackMonths), start))
            salaryEntries.AddRange(GetSalaryEntries(account, salaryLabel).Where(x => x.PostingDate < start));

        if (salaryEntries.Count == 0) return 0;

        // Sum the whole latest salary month rather than taking the single latest entry, so split
        // payouts (e.g. bi-weekly) still add up to one monthly salary.
        var latestSalaryDate = salaryEntries.Max(x => x.PostingDate);
        return salaryEntries
            .Where(x => x.PostingDate.Year == latestSalaryDate.Year && x.PostingDate.Month == latestSalaryDate.Month)
            .Sum(x => x.ValueChange);
    }

    private static decimal SumSalary(CurrencyAccount account, FinancialLabel salaryLabel) =>
        GetSalaryEntries(account, salaryLabel).Sum(x => x.ValueChange);

    private static IEnumerable<CurrencyAccountEntry> GetSalaryEntries(CurrencyAccount account, FinancialLabel salaryLabel) =>
        account.Entries.Where(x => x.Labels is not null && x.Labels.Any(y => y.Id == salaryLabel.Id));
}