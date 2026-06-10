using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.FinancialAccounts.Currencies;
using FinanceManager.Domain.Entities.Stocks;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Repositories.Account;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.MoneyFlow.InvestmentRate;

public class InvestmentRateService(IFinancialAccountRepository financialAccountRepository, IFinancialLabelsRepository financialLabelsRepository,
IStockPriceProvider stockPriceProvider) : IInvestmentRateService
{
    public async IAsyncEnumerable<Domain.Entities.MoneyFlowModels.InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end)
    {
        var labels = await financialLabelsRepository.GetLabels().ToListAsync();
        var salaryLabel = labels.Single(x => x.Name.ToLower() == "salary");

        Currency currency = DefaultCurrency.PLN; // TODO: use user currency settings

        decimal salary = 0;
        await foreach (var account in financialAccountRepository.GetAccounts<CurrencyAccount>(userId, start, end))
            salary += account.Entries.Where(x => x.Labels is not null && x.Labels.Any(y => y.Id == salaryLabel.Id)).Sum(x => x.ValueChange);

        if (salary == 0) yield break;

        decimal investmentsChange = 0;
        await foreach (var account in financialAccountRepository.GetAccounts<StockAccount>(userId, start, end))
            foreach (var entry in account.Entries)
                investmentsChange += entry.ValueChange * await stockPriceProvider.GetPricePerUnitAsync(entry.Isin, currency, entry.PostingDate);

        yield return new()
        {
            Start = start,
            End = end,
            Salary = salary,
            InvestmentsChange = investmentsChange
        };
    }
}