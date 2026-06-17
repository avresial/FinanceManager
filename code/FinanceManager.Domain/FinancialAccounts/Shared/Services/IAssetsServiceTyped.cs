namespace FinanceManager.Domain.FinancialAccounts.Shared.Services;

public interface IAssetsServiceTyped : IAssetsService
{
    bool IsOfType<T>();
}