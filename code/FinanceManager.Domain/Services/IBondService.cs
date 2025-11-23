using FinanceManager.Domain.Entities.Bonds;

namespace FinanceManager.Domain.Services;

public interface IBondService
{
    Task<bool> AddCalculationMethodAsync(int bondDetailsId, BondCalculationMethod calculationMethod, CancellationToken cancellationToken = default);
    Task<bool> RemoveCalculationMethodAsync(int bondDetailsId, int calculationMethodId, CancellationToken cancellationToken = default);
}
