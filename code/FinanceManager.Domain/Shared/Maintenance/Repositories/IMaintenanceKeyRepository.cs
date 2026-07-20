using FinanceManager.Domain.Shared.Maintenance.Entities;

namespace FinanceManager.Domain.Shared.Maintenance.Repositories;

/// <summary>Persistence for the single active <see cref="MaintenanceApiKey"/> row.</summary>
public interface IMaintenanceKeyRepository
{
    /// <summary>Get the active key, or <c>null</c> when none has been generated (or it was revoked).</summary>
    Task<MaintenanceApiKey?> Get(CancellationToken cancellationToken = default);

    /// <summary>Store the key, replacing any existing one — generating and rolling are the same write.</summary>
    Task Save(MaintenanceApiKey key, CancellationToken cancellationToken = default);

    /// <summary>Delete the active key. Returns <c>false</c> when there was none to revoke.</summary>
    Task<bool> Delete(CancellationToken cancellationToken = default);
}