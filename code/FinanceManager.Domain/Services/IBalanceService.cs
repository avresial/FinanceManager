using FinanceManager.Domain.Entities.Currencies;
using FinanceManager.Domain.Entities.MoneyFlowModels;

namespace FinanceManager.Domain.Services;

public interface IBalanceService
{
    Task<List<TimeSeriesModel>> GetIncome(int userId, Currency currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetSpending(int userId, Currency currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetBalance(int userId, Currency currency, DateTime start, DateTime end);
}
