using FinanceManager.Domain.Entities.Currencies;

namespace FinanceManager.Domain.Services;

public interface INetWorthService
{
    Task<decimal?> GetNetWorth(int userId, Currency currency, DateTime date);
    Task<Dictionary<DateTime, decimal>> GetNetWorth(int userId, Currency currency, DateTime start, DateTime end);
}