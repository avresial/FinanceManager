using FinanceManager.Domain.Transactions.Bonds.Entities;

namespace FinanceManager.Domain.Transactions.Bonds.Services;

public interface IBondService
{
    Task<bool> AddCalculationMethodAsync(int bondDetailsId, BondCalculationMethod calculationMethod, CancellationToken cancellationToken = default);
    Task<bool> RemoveCalculationMethodAsync(int bondDetailsId, int calculationMethodId, CancellationToken cancellationToken = default);
}