using FinanceManager.Domain.Entities.Bonds;
using FinanceManager.Domain.Repositories;
using FinanceManager.Infrastructure.Contexts;
using Microsoft.EntityFrameworkCore;

namespace FinanceManager.Infrastructure.Repositories;

internal class BondDetailsRepository(AppDbContext context) : IBondDetailsRepository
{
    public async Task<int> AddAsync(BondDetails bond, CancellationToken cancellationToken = default)
    {
        context.Bonds.Add(bond);
        await context.SaveChangesAsync(cancellationToken);
        return bond.Id;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var existing = await context.Bonds.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (existing is null) return false;
        context.Bonds.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public IAsyncEnumerable<BondDetails> GetAllAsync(CancellationToken cancellationToken = default)
        => context.Bonds.Include(b => b.CalculationMethods).AsAsyncEnumerable();

    public async Task<BondDetails?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
        => await context.Bonds.Include(b => b.CalculationMethods).FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public async Task<IReadOnlyList<BondDetails>> GetByIssuerAsync(string issuer, CancellationToken cancellationToken = default)
        => await context.Bonds.Include(b => b.CalculationMethods).Where(x => x.Issuer == issuer).ToListAsync(cancellationToken);

    public async Task<bool> UpdateAsync(BondDetails bond, CancellationToken cancellationToken = default)
    {
        context.Update(bond);

        await context.SaveChangesAsync(cancellationToken);
        return true;
    }
}