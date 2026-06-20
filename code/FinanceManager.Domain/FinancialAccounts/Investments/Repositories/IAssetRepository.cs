using FinanceManager.Domain.FinancialAccounts.Investments.Entities;

namespace FinanceManager.Domain.FinancialAccounts.Investments.Repositories;

public interface IAssetRepository
{
    Task<Asset?> Get(long id, CancellationToken cancellationToken = default);
    Task<Asset?> GetByIsin(string isin, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Asset>> GetAll(CancellationToken cancellationToken = default);
    Task<Asset> Add(Asset asset, CancellationToken cancellationToken = default);

    /// <summary>Insert the asset, or update the existing one matched by ISIN, returning the persisted entity.</summary>
    Task<Asset> Upsert(Asset asset, CancellationToken cancellationToken = default);
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);
}