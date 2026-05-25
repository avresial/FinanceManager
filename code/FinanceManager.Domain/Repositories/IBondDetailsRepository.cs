using FinanceManager.Domain.Entities.Bonds;

namespace FinanceManager.Domain.Repositories;

public interface IBondDetailsRepository
{
    Task<BondDetails?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    IAsyncEnumerable<BondDetails> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BondDetails>> GetByIssuerAsync(string issuer, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetIssuersAsync(CancellationToken cancellationToken = default);
    Task<int> AddAsync(BondDetails bond, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(BondDetails bond, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}