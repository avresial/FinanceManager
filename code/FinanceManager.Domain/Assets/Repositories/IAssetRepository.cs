using FinanceManager.Domain.Assets.Entities;

namespace FinanceManager.Domain.Assets.Repositories;

public interface IAssetRepository
{
    /// <summary>Get an asset by its database id, or <c>null</c> if none exists.</summary>
    Task<Asset?> Get(long id, CancellationToken cancellationToken = default);

    /// <summary>Get the asset whose <see cref="Asset.Isin"/> matches, or <c>null</c> if none exists.</summary>
    Task<Asset?> GetByIsin(string isin, CancellationToken cancellationToken = default);

    /// <summary>Get the asset whose <see cref="Asset.ShareClassFigi"/> matches, or <c>null</c> if none exists.</summary>
    Task<Asset?> GetByShareClassFigi(string shareClassFigi, CancellationToken cancellationToken = default);

    /// <summary>Get all assets.</summary>
    Task<IReadOnlyList<Asset>> GetAll(CancellationToken cancellationToken = default);

    /// <summary>Insert a new asset and return the persisted entity (with its generated id).</summary>
    Task<Asset> Add(Asset asset, CancellationToken cancellationToken = default);

    /// <summary>
    /// Insert the asset, or update the existing one matched by ISIN (when present) or otherwise by
    /// share-class FIGI, returning the persisted entity.
    /// </summary>
    Task<Asset> Upsert(Asset asset, CancellationToken cancellationToken = default);

    /// <summary>Update an existing asset's editable fields by id (CreatedAt and listings untouched). Returns <c>false</c> when none exists.</summary>
    Task<bool> Update(Asset asset, CancellationToken cancellationToken = default);

    /// <summary>Delete the asset with the given id. Returns <c>false</c> when no such asset exists.</summary>
    Task<bool> Delete(long id, CancellationToken cancellationToken = default);
}