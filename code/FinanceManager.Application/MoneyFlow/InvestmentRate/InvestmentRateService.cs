using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Entities;
using FinanceManager.Domain.FinancialAccounts.Investments.Services;
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
    public async IAsyncEnumerable<Domain.MoneyFlow.Entities.InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end)
    {
        var labels = await financialLabelsRepository.GetLabels().ToListAsync();
        var salaryLabel = labels.Single(x => x.Name.ToLower() == "salary");

        Currency currency = DefaultCurrency.PLN; // TODO: use user currency settings

        decimal salary = 0;
        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
            salary += account.Entries.Where(x => x.Labels is not null && x.Labels.Any(y => y.Id == salaryLabel.Id)).Sum(x => x.ValueChange);

        if (salary == 0) yield break;

        decimal investmentsChange = 0;
        await foreach (var account in financialAccountRepository.GetAccounts<InvestmentAccount>(userId, start, end))
        {
            var startValue = await investmentValuationService.GetAccountValueAsync(account.AccountId, currency, start);
            var endValue = await investmentValuationService.GetAccountValueAsync(account.AccountId, currency, end);
            investmentsChange += endValue - startValue;
        }

        yield return new()
        {
            Start = start,
            End = end,
            Salary = salary,
            InvestmentsChange = investmentsChange
        };
    }
}