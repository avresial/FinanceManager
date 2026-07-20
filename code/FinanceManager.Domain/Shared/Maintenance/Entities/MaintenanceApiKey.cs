namespace FinanceManager.Domain.Shared.Maintenance.Entities;

/// <summary>
/// The single active maintenance API key, stored as a SHA-256 hash so a database leak never
/// reveals the key itself. The plaintext is shown to the admin exactly once at generation time;
/// rolling the key replaces this row, revoking deletes it.
/// </summary>
public class MaintenanceApiKey
{
    public int Id { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}