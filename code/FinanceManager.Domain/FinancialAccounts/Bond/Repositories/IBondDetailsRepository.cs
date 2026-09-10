using FinanceManager.Domain.FinancialAccounts.Bond.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Bond.Repositories;

public interface IBondDetailsRepository
{
    Task<BondDetails?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    /// <summary>
    /// Resolves bond names for a set of ids in one query. Missing ids are omitted so callers can retain
    /// the dashboard's existing fallback description for deleted bond details.
    /// </summary>
    Task<IReadOnlyDictionary<int, string>> GetNamesByIdsAsync(IReadOnlyCollection<int> ids, CancellationToken cancellationToken = default);
    IAsyncEnumerable<BondDetails> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BondDetails>> GetByIssuerAsync(string issuer, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<string>> GetIssuersAsync(CancellationToken cancellationToken = default);
    Task<int> AddAsync(BondDetails bond, CancellationToken cancellationToken = default);
    Task<bool> UpdateAsync(BondDetails bond, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(int id, CancellationToken cancellationToken = default);
}