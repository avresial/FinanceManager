using FinanceManager.Domain.Entities.Currencies;

namespace FinanceManager.Domain.MoneyFlow.Services;

public interface INetWorthService
{
    Task<decimal?> GetNetWorth(int userId, Currency currency, DateTime date);
    Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, Currency currency, DateTime start, DateTime end);
}