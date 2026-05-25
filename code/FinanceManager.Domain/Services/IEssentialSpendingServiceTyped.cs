namespace FinanceManager.Domain.Services;

public interface IEssentialSpendingServiceTyped : IEssentialSpendingService
{
    bool IsOfType<T>();
}