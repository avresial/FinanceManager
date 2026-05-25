namespace FinanceManager.Domain.Services;

public interface IExpenseDistributionServiceTyped : IExpenseDistributionService
{
    bool IsOfType<T>();
}