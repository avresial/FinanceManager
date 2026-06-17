using FinanceManager.Domain.FinancialAccounts.Bond.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Services;

public interface IBondService
{
    Task<bool> AddCalculationMethodAsync(int bondDetailsId, BondCalculationMethod calculationMethod, CancellationToken cancellationToken = default);
    Task<bool> RemoveCalculationMethodAsync(int bondDetailsId, int calculationMethodId, CancellationToken cancellationToken = default);
}