using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;
public interface IMoneyFlowService
{
    Task<decimal?> GetNetWorth(int userId, string currency, DateTime date);
    Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, string currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetIncome(int userId, string currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetSpending(int userId, string currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetBalance(int userId, string currency, DateTime start, DateTime end);
    Task<List<NameValueResult>> GetLabelsValue(int userId, DateTime start, DateTime end);
    IAsyncEnumerable<InvestmentRate> GetInvestmentRate(int userId, DateTime start, DateTime end);
}
