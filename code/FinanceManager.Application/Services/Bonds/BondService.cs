using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Repositories;
using FinanceManager.Domain.Services;

namespace FinanceManager.Application.Services;

public class BondService(IBondDetailsRepository bondDetailsRepository) : IBondService
{
    public async Task<bool> AddCalculationMethodAsync(int bondDetailsId, BondCalculationMethod calculationMethod, CancellationToken cancellationToken = default)
    {
        var bond = await bondDetailsRepository.GetByIdAsync(bondDetailsId, cancellationToken);
        if (bond is null)
            return false;

        var methodWithBackRef = calculationMethod with { BondDetails = bond };
        bond.CalculationMethods.Add(methodWithBackRef);

        return await bondDetailsRepository.UpdateAsync(bond, cancellationToken);
    }

    public async Task<bool> RemoveCalculationMethodAsync(int bondDetailsId, int calculationMethodId, CancellationToken cancellationToken = default)
    {
        var bond = await bondDetailsRepository.GetByIdAsync(bondDetailsId, cancellationToken);
        if (bond is null)
            return false;

        var method = bond.CalculationMethods.FirstOrDefault(m => m.Id == calculationMethodId);
        if (method is null)
            return false;

        bond.CalculationMethods.Remove(method);

        return await bondDetailsRepository.UpdateAsync(bond, cancellationToken);
    }
}