using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;

public interface IMoneyFlowService
{
    Task<decimal?> GetNetWorth(int userId, Currency currency, DateTime date);
    Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, Currency currency, DateTime start, DateTime end);
    Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end);
    IAsyncEnumerable<InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end);
}