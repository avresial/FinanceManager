namespace FinanceManager.Application.Shared.Maintenance;

/// <summary>Current state of the maintenance key, safe to show an admin (never the key itself).</summary>
/// <param name="HasKey">Whether an admin-generated key is active in the database.</param>
/// <param name="CreatedAt">When the active key was generated, or <c>null</c> when none exists.</param>
public record MaintenanceKeyStatus(bool HasKey, DateTimeOffset? CreatedAt);

/// <summary>
/// Manages the shared secret that guards the maintenance endpoints (e.g. the weekly price
/// backfill trigger). The admin generates, rolls, or revokes the key at runtime; the plaintext is
/// returned exactly once at generation and only its SHA-256 hash is stored. A key supplied via
/// configuration remains valid as a break-glass fallback alongside the database key.
/// </summary>
public interface IMaintenanceKeyService
{
    /// <summary>Generate a new key, replacing any existing one (rolling). Returns the plaintext — the only time it is ever available.</summary>
    Task<string> GenerateAsync(CancellationToken ct = default);

    /// <summary>Revoke the active database key. Returns <c>false</c> when there was none.</summary>
    Task<bool> RevokeAsync(CancellationToken ct = default);

    /// <summary>Status of the database key for the admin UI.</summary>
    Task<MaintenanceKeyStatus> GetStatusAsync(CancellationToken ct = default);

    /// <summary>Whether any key (database or configuration) is set — when not, maintenance endpoints answer 404.</summary>
    Task<bool> IsConfiguredAsync(CancellationToken ct = default);

    /// <summary>Constant-time check of a caller-supplied key against the database key and the configuration fallback.</summary>
    Task<bool> ValidateAsync(string providedKey, CancellationToken ct = default);
}