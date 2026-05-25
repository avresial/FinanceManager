namespace FinanceManager.Domain.Services;

public interface IBalanceServiceTyped : IBalanceService
{
    bool IsOfType<T>();
}