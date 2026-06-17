namespace FinanceManager.Domain.MoneyFlow.Services;

public interface IEssentialSpendingServiceTyped : IEssentialSpendingService
{
    bool IsOfType<T>();
}