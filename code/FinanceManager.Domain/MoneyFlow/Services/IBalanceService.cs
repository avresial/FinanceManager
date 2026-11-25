using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.MoneyFlow.Services;

public interface IBalanceService
{
    Task<List<TimeSeriesModel>> GetInflow(int userId, Currency currency, DateTime start, DateTime end);

    Task<List<TimeSeriesModel>> GetOutflow(int userId, Currency currency, DateTime start, DateTime end);

    Task<List<TimeSeriesModel>> GetNetCashFlow(int userId, Currency currency, DateTime start, DateTime end);

    Task<List<TimeSeriesModel>> GetCapital(int userId, Currency currency, DateTime start, DateTime end);

    Task<List<TimeSeriesModel>> GetClosingBalance(int userId, Currency currency, DateTime start, DateTime end);
}