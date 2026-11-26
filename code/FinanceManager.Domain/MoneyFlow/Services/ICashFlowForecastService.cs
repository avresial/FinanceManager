using FinanceManager.Domain.Currencies.Entities;
using FinanceManager.Domain.MoneyFlow.Entities;

namespace FinanceManager.Domain.MoneyFlow.Services;

public interface ICashFlowForecastService
{
    Task<CashFlowForecast> GetForecast(
        int userId,
        Currency currency,
        int horizonDays,
        CancellationToken cancellationToken = default);
}