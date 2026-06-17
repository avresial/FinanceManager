using FinanceManager.Domain.FinancialAccounts.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.MoneyFlow.Services;

public interface IEssentialSpendingService
{
    Task<List<TimeSeriesModel>> GetEssentialSpending(int userId, Currency currency, DateTime start, DateTime end);
    Task<List<TimeSeriesModel>> GetEssentialSpending(int userId, Currency currency, DateTime start, DateTime end, IReadOnlyCollection<int> accountIds);
}