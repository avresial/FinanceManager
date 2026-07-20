using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.MoneyFlow.Services;

public interface IInvestmentRateService
{
    IAsyncEnumerable<InvestmentRate> GetInvestmentRate(int userId, Currency currency, DateTime start, DateTime end);
}