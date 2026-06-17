namespace FinanceManager.Domain.FinancialAccounts.Shared.Services;

public interface IBalanceServiceTyped : IBalanceService
{
    bool IsOfType<T>();
}