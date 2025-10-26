namespace FinanceManager.Domain.Services;

public interface IAssetsServiceTyped : IAssetsService
{
    bool IsOfType<T>();
}

